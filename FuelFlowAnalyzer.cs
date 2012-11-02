using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

//Fuel flow == (Thrust * 200)/(Isp * 9.81)

namespace MuMech
{
    class FuelFlowAnalyzer
    {
        public enum Environment { ATMOSPHERE, VACUUM };


        int simStage;
        List<FuelNode> nodes; //a list of FuelNodes representing all the parts of the ship

        List<Part> parts;

        //assemble the representation of the ship in terms of a set of FuelNodes and fuel source relationships
        public void rebuildFuelFlowGraph(Environment enviro)
        {
            nodes = new List<FuelNode>();

            //a useful tool to let us look up the fuel node corresponding to a given part:
            Dictionary<Part, FuelNode> nodeLookup = new Dictionary<Part, FuelNode>();

            //create a FuelNode for each part
            foreach (Part p in parts)
            {
                FuelNode n = new FuelNode(p, enviro);
                nodes.Add(n);
                nodeLookup[p] = n;
            }

            //figure out where each FuelNode can draw fuel from
            foreach (FuelNode n in nodes)
            {
                //solid rockets can only draw on internal fuel
                if (n.part is SolidRocket)
                {
                    n.addSource(n);
                    continue; //skip all the code below, which applies only to liquid fuel
                }

                //first draw fuel from any fuel lines that point to this part
                foreach (FuelNode m in nodes)
                {
                    if (m.part is FuelLine && ((FuelLine)m.part).target == n.part)
                    {
                        n.addSource(m);
                    }
                }

                //then draw fuel from stacked parts
                foreach (AttachNode attachNode in n.part.attachNodes)
                {
                    //decide if it's possible to draw fuel through this node:
                    if (attachNode.attachedPart != null                                 //if there is a part attached here            
                        && attachNode.nodeType == AttachNode.NodeType.Stack             //and the attached part is stacked (rather than surface mounted)
                        && (attachNode.attachedPart.fuelCrossFeed                       //and the attached part allows fuel flow
                            || attachNode.attachedPart is FuelTank)                     //    or the attached part is a fuel tank
                        && !(n.part.NoCrossFeedNodeKey.Length > 0                       //and this part does not forbid fuel flow
                             && attachNode.id.Contains(n.part.NoCrossFeedNodeKey)))     //    through this particular node
                    {
                        n.addSource(nodeLookup[attachNode.attachedPart]);
                    }
                }

                //then draw fuel from the parent part. For example, fuel tanks can draw
                //fuel from a fuel tank to which they are surface mounted.
                if (n.part.parent != null && n.part.parent.fuelCrossFeed)
                {
                    n.addSource(nodeLookup[n.part.parent]);
                }
            }
        }

        //analyze the whole ship and report a) burn time per stage and b) delta V per stage
        public void analyze(List<Part> parts, double gravity, Environment enviro, 
                            out float[] timePerStage, out float[] deltaVPerStage, out float[] twrPerStage)
        {
            this.parts = parts;

            //reinitialize our representation of the vessel
            rebuildFuelFlowGraph(enviro);
            simStage = Staging.lastStage;

            timePerStage = new float[simStage + 1];
            deltaVPerStage = new float[simStage + 1];
            twrPerStage = new float[simStage + 1];

            //simulate fuel consumption until all the stages have been executed
            while (simStage >= 0)
            {
                //print("starting stage " + simStage);

                //beginning of stage # simStage
                float stageTime = 0;
                float stageDeltaV = 0;
                float stageTWR;

                //make a list of the engines that are active during this stage
                //what if an engine burns out mid-stage, but we don't stage until a different engine burns out?
                List<FuelNode> engines = findActiveEngines();

                //sum up the thrust of all engines active during this stage
                float totalStageThrust = 0;
                foreach (FuelNode engine in engines) totalStageThrust += engine.thrust;

                stageTWR = (float)(totalStageThrust / (totalShipMass() * gravity));

                int deadmanSwitch = 1000;
                while (!allowedToStage()) //simulate chunks of time until this stage burns out
                {
                    //recompute the list of active engines and their thrust, in case some burn out mid-stage:
                    engines = findActiveEngines();
                    totalStageThrust = 0;
                    foreach (FuelNode engine in engines) totalStageThrust += engine.thrust;

                    //figure the rate at which fuel is draining from each node:
                    assignFuelDrainRates();

                    //find how long it will be until some node runs out of fuel:
                    float minFuelDrainTime = 999999999;
                    foreach (FuelNode n in nodes)
                    {
                        if (n.fuelDrainRate > 0 && n.fuel / n.fuelDrainRate < minFuelDrainTime) minFuelDrainTime = n.fuel / n.fuelDrainRate;
                    }

                    //advance time until some fuel node is emptied (because nothing exciting happens before then)
                    float dt = minFuelDrainTime;
                    float startMass = totalShipMass();
                    foreach (FuelNode n in nodes) n.fuel -= n.fuelDrainRate * dt;
                    float endMass = totalShipMass();
                    stageTime += dt;

                    //print("dt = " + dt);

                    //calculate how much dV was produced during this time step
                    if (dt > 0 && startMass > endMass && startMass > 0 && endMass > 0)
                    {
                        stageDeltaV += totalStageThrust * dt / (startMass - endMass) * Mathf.Log(startMass / endMass);
                    }

                    deadmanSwitch--;
                    if (deadmanSwitch <= 0)
                    {
                        //print("dead man switch activated at stage " + simStage + " !!!!");
                        break; //in case we get stuck in an infinite loop due to unanticipated staging logic
                    }
                }

                //record the stats computed for this stage
                timePerStage[simStage] = stageTime;
                deltaVPerStage[simStage] = stageDeltaV;
                twrPerStage[simStage] = stageTWR;

                //advance to the next stage
                simStage--;
                simulateStageActivation();
            }
        }

        bool allowedToStage()
        {
            List<FuelNode> activeEngines = findActiveEngines();

            //if no engines are active, we can always stage
            if (activeEngines.Count == 0) return true;

            //if staging would decouple an active engine or non-empty fuel tank, we're not allowed to stage
            foreach (FuelNode n in nodes)
            {
                if (n.decoupledInStage == (simStage - 1))
                {
                    if (n.fuel > 1.0F || activeEngines.Contains(n))
                    {
                        return false;
                    }
                }
            }

            //if this isn't the last stage, we're allowed to stage
            if (simStage > 0) return true;

            //if this is the last stage, we're not allowed to stage (finish) unless there are no active engines remaining
            foreach (FuelNode n in nodes)
            {
                if (n.isEngine && n.sources.Count > 0) return false;
            }

            //if this is the last stage and there are no active engines remaining, we can stage
            return true;
        }

        //remove all nodes that get decoupled in the current stage
        void simulateStageActivation()
        {
            List<FuelNode> decoupledNodes = new List<FuelNode>();
            foreach (FuelNode n in nodes)
            {
                if (n.decoupledInStage == simStage) decoupledNodes.Add(n);
            }

            foreach (FuelNode n in decoupledNodes)
            {
                killSource(n); //decoupled nodes can no longer supply fuel to any other node
                nodes.Remove(n); //remove the decoupled node from the simulated ship
            }
        }

        //Sum the mass of all fuel nodes in the simulated ship.
        //FuelNodes dynamically recompute their mass as they lose fuel during the simulation.
        float totalShipMass()
        {
            float ret = 0;
            foreach (FuelNode node in nodes) ret += node.mass;
            return ret;
        }

        //Returns a list of engines that fire during the current simulated stage.
        List<FuelNode> findActiveEngines()
        {
            List<FuelNode> engines = new List<FuelNode>();
            foreach (FuelNode node in nodes)
            {
                if (node.isEngine && node.part.inverseStage >= simStage && node.canDrawFuel())
                {
                    engines.Add(node);
                }
            }

            return engines;
        }

        //Figure out how much fuel drains from each node per unit time.
        //We do this by finding which engines are active, and then having
        //them figure out where they draw fuel from and at what rate.
        void assignFuelDrainRates()
        {
            foreach (FuelNode n in nodes) n.fuelDrainRate = 0;

            List<FuelNode> engines = findActiveEngines();

            foreach (FuelNode engine in engines) engine.assignFuelDrainRates();
        }

        /*
        //Find fuel nodes that are expected to supply fuel but can't. These nodes
        //have run out of fuel and will never be able to supply fuel again. Remove them 
        //as possible sources for other fuel nodes. This way, when an engine no longer
        //has any sources, we know it has run out of fuel.
        void killEmptySources(List<FuelNode> engines)
        {
            bool sourceKilled = true;
            while (sourceKilled)
            {
                //figure out where fuel is being drained from
                assignFuelDrainRates();

                //check if any nodes are expected to supply fuel but can't. if so they have been
                //drained; remove them from the fuel flow graph
                sourceKilled = false;
                foreach (FuelNode n in nodes)
                {
                    if (n.fuelDrainRate > 0 && n.fuel < 1.0F)
                    {
                        if (killSource(n)) sourceKilled = true;
                    }
                }

                //If we killed a source, it might have been at the end of a long fuel chain. We may now need
                //to kill some or all of the rest of the chain. Hence the while loop.
            }
        }
        */

        //Remove the given FuelNode from any source lists in which it appears.
        //Should be called when the given FuelNode becomes no longer capable of supplying
        //fuel to anyone. Returns true if the given FuelNode was actually removed from
        //at least one source list.
        bool killSource(FuelNode source)
        {
            bool wasSource = false;

            source.fuel = 0;

            foreach (FuelNode n in nodes)
            {
                if (n.sources.Contains(source))
                {
                    n.sources.Remove(source);
                    wasSource = true;
                }
            }

            return wasSource;
        }
        


        public static void print(String s)
        {
            MonoBehaviour.print(s);
        }
    }

    class FuelNode
    {
        public List<FuelNode> sources = new List<FuelNode>();

        public Part part;
        public float fuel;
        public float thrust;
        public float fuelConsumption;
        public float fuelDrainRate;
        public int decoupledInStage = -1;

        public FuelNode(Part part, FuelFlowAnalyzer.Environment enviro)
        {
            this.part = part;

            if (part is FuelTank)
            {
                fuel = ((FuelTank)part).fuel;
            }

            if (part is SolidRocket)
            {
                if (!part.ActivatesEvenIfDisconnected) //if ActivatesEvenIfDisconnected, this is probably a separatron, not a motor.
                {
                    fuel = ((SolidRocket)part).internalFuel;
                    thrust = ((SolidRocket)part).thrust;
                    fuelConsumption = ((SolidRocket)part).fuelConsumption;
                }
            }

            if (part is LiquidEngine)
            {
                thrust = ((LiquidEngine)part).maxThrust;
                fuelConsumption = ((LiquidEngine)part).fuelConsumption;
            }

            if (part is LiquidFuelEngine)
            {
                thrust = ((LiquidFuelEngine)part).maxThrust;
                //Fuel flow == (Thrust * 200)/(Isp * 9.81)
                if (enviro == FuelFlowAnalyzer.Environment.ATMOSPHERE)
                {
                    fuelConsumption = thrust * 200 / (((LiquidFuelEngine)part).Isp * 9.81f);
                }
                else //enviro == FuelFlowAnalyzer.Environment.VACUUM
                {
                    fuelConsumption = thrust * 200 / (((LiquidFuelEngine)part).vacIsp * 9.81f);
                }
            }

            //figure out when this part gets decoupled
            Part p = part;
            while (p != null)
            {
                if (p is Decoupler || p is DecouplerGUI || p is RadialDecoupler)
                {
                    decoupledInStage = p.inverseStage;
                    break;
                }
                else if (p.parent == null)
                {
                    decoupledInStage = -1; //the root part is never decoupled.
                    break;
                }
                else
                {
                    p = p.parent;
                }
            }
        }

        //return the mass of the simulated FuelNode. This is not the same as the mass of the Part,
        //because the simulated node may have lost fuel, and thus mass, during the simulation.
        public float mass
        {
            get
            {
                //some parts have no physical significance and KSP ignores their mass parameter:
                if (part.physicalSignificance == Part.PhysicalSignificance.NONE) return 0.0F;

                //Solid rockets and fuel tanks have masses that vary with their current fuel content.
                //We compute the simulated mass of the FuelNode by using the simulated fuel content to
                //interpolate between the dry mass and the mass of the part at the actual current game time.
                //We would interpolate between the dry mass and the full mass, but you can't actually determine
                //the original full mass if the part has already drained some fuel.
                if (part is SolidRocket)
                {
                    if (((SolidRocket)part).internalFuel == 0) return ((SolidRocket)part).dryMass;
                    else return Mathf.Lerp(((SolidRocket)part).dryMass, part.mass, this.fuel / ((SolidRocket)part).internalFuel);
                }
                if (part is FuelTank)
                {
                    if (((FuelTank)part).fuel == 0) return ((FuelTank)part).dryMass;
                    else return Mathf.Lerp(((FuelTank)part).dryMass, part.mass, this.fuel / ((FuelTank)part).fuel);
                }
                return part.mass;
            }
        }

        public bool isEngine
        {
            get { return fuelConsumption > 0; }
        }

        public void addSource(FuelNode source)
        {
            if (!sources.Contains(source)) sources.Add(source);
        }

        public void assignFuelDrainRates()
        {
            if(part is SolidRocket) 
            {
                //solid rockets only drain their own fuel
                fuelDrainRate = fuelConsumption;
            }
            else if(this.fuelConsumption > 0) 
            {
                //liquid engines use the full-blown recursive fuel flow system:
                this.assignFuelDrainRates(this.fuelConsumption, new List<FuelNode>());
            }
        }

        //used to check whether engines have burned out, by checking to see whether
        //they can still draw fuel from somewhere
        public bool canDrawFuel()
        {
            List<FuelNode> visited = new List<FuelNode>();
            visited.Add(this);
            foreach(FuelNode n in sources) {
                if(n.canSupplyFuel(visited)) return true;
            }

            return false;
        }

        //determine if this FuelNode can supply fuel itself, or can supply fuel by drawing
        //from other sources, without drawing through any node in <visited>
        bool canSupplyFuel(List<FuelNode> visited)
        {
            if (this.fuel > 1.0F) return true;

            //if we drain from our sources, newVisted is the set of nodes that those sources
            //aren't allowed to drain from. We add this node to that list to prevent loops.
            List<FuelNode> newVisited = new List<FuelNode>(visited);
            newVisited.Add(this);

            foreach (FuelNode n in sources)
            {
                if (!visited.Contains(n))
                {
                    if (n.canSupplyFuel(newVisited)) return true;
                }
            }

            return false;
        }

        //We need to drain <totalDrainRate> fuel per second from somewhere.
        //We're not allowed to drain it through any of the nodes in <visited>.
        //Decide whether to drain it from this node, or pass the recursive buck
        //and drain it from some subset of the sources of this node.
        void assignFuelDrainRates(float totalDrainRate, List<FuelNode> visited)
        {
            //if we drain from our sources, newVisted is the set of nodes that those sources
            //aren't allowed to drain from. We add this node to that list to prevent loops.
            List<FuelNode> newVisited = new List<FuelNode>(visited);
            newVisited.Add(this);

            //First see if we can drain fuel through fuel lines. If we can, drain equally through
            //all active fuel lines that point to this part. 
            List<FuelNode> fuelLines = new List<FuelNode>();
            foreach (FuelNode n in sources)
            {
                if (n.part is FuelLine && !visited.Contains(n) && n.canSupplyFuel(newVisited)) fuelLines.Add(n);
            }
            if (fuelLines.Count > 0)
            {
                foreach (FuelNode fuelLine in fuelLines)
                {
                    fuelLine.assignFuelDrainRates(totalDrainRate / fuelLines.Count, newVisited);
                }
                return;
            }

            //If there are no incoming fuel lines, try other sources.
            //I think there may actually be more structure to the fuel source priority system here. 
            //For instance, can't a fuel tank drain fuel simultaneously from its top and bottom stack nodes?
            foreach (FuelNode n in sources)
            {
                if (!visited.Contains(n) && n.canSupplyFuel(newVisited))
                {
                    if (drainFromSourceBeforeSelf(n))
                    {
                        n.assignFuelDrainRates(totalDrainRate, newVisited);
                        return;
                    }
                }
            }

            //in the final extremity, drain fuel from this part
            if(this.fuel > 0) fuelDrainRate += totalDrainRate;
        }

        //If we still have fuel, don't drain through the parent unless the parent node is a stack node.
        //This just seems to be an idiosyncracy of the KSP fuel flow system, which we faithfully simulate.
        bool drainFromSourceBeforeSelf(FuelNode source)
        {
            if (this.fuel == 0) return true;
            if (source.part != this.part.parent) return true;
            if (this.part.parent == null) return true;

            foreach (AttachNode attachNode in this.part.parent.attachNodes)
            {
                if (attachNode.attachedPart == this.part && attachNode.nodeType != AttachNode.NodeType.Stack) return false;
            }

            return true;
        }
    }
}
