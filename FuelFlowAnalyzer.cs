using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace MuMech
{
    class FuelFlowAnalyzer
    {
        int simStage;
        List<FuelNode> nodes;
        Dictionary<Part, FuelNode> nodeLookup;

        Vessel vessel;

        public FuelFlowAnalyzer(Vessel vessel)
        {
            this.vessel = vessel;
        }

        public void rebuildFuelFlowGraph()
        {
            //print("--");

            nodes = new List<FuelNode>();
            nodeLookup = new Dictionary<Part, FuelNode>();

            //print("a");

            //create a FuelNode for each part
            foreach (Part p in vessel.parts)
            {
                FuelNode n = new FuelNode(p);
                nodes.Add(n);
                nodeLookup[p] = n;
            }

            //print("b");

            //figure out where each FuelNode can draw fuel from
            foreach (FuelNode n in nodes)
            {
                //solid rockets can only draw on internal fuel
                if (n.part is SolidRocket)
                {
                    n.addSource(n);
                    continue;
                }

                //first draw fuel from any fuel lines that point to this part
                foreach (FuelNode m in nodes)
                {
                    if (m.part is FuelLine && ((FuelLine)m.part).target == n.part)
                    {
                        //                       print("" + n.part + " can draw fuel through fuel line " + m.part);
                        n.addSource(m);
                    }
                }

                //then draw fuel from stacked parts
                foreach (AttachNode attachNode in n.part.attachNodes)
                {
                    //print("attachNode.nodetype = " + attachNode.nodeType);
                    if (attachNode.attachedPart != null
                        && attachNode.nodeType == AttachNode.NodeType.Stack
                        && (attachNode.attachedPart.fuelCrossFeed || attachNode.attachedPart is FuelTank)
                        && !(n.part.NoCrossFeedNodeKey.Length > 0 && attachNode.id.Contains(n.part.NoCrossFeedNodeKey)))
                    {
                        //                       print("" + n.part + " can draw fuel from stacked part " + attachNode.attachedPart);
                        n.addSource(nodeLookup[attachNode.attachedPart]);
                    }
                }

                //then draw fuel from the parent
                if (n.part.parent != null && n.part.parent.fuelCrossFeed)
                {
                    //                   print("" + n.part + " can draw fuel from parent " + n.part.parent);
                    //                   print("n.part.parent.FindAttachNodeByPart(n.part) == " + n.part.parent.findAttachNodeByPart(n.part));
                    n.addSource(nodeLookup[n.part.parent]);
                }
            }
        }

        public void analyze(out float[] timePerStage, out float[] deltaVPerStage)
        {
            print("--");

            rebuildFuelFlowGraph();

            simStage = Staging.CurrentStage;

            print("starting at stage " + simStage + "; initial mass = " + totalShipMass());

            timePerStage = new float[simStage + 1];
            deltaVPerStage = new float[simStage + 1];

            while (simStage >= 0)
            {
//                                print("stage " + simStage);

                float stageTime = 0;
                float stageDeltaV = 0;

                List<FuelNode> engines = findActiveEngines(simStage);

                float totalStageThrust = 0;
                foreach (FuelNode engine in engines)
                {
                    totalStageThrust += engine.thrust;
                }

//                                print("" + engines.Count + " engines active");

                float t = 0;
                bool doneStage = false;
                if (allowedToStage2()) doneStage = true; //check if we can skip through this stage in zero time
                while (!doneStage)
                {
                    assignFuelDrainRates();

                    //find which node will run out of fuel first:
                    float minFuelDrainTime = 999999999;
                    foreach (FuelNode n in nodes)
                    {
                        if (n.fuelDrainRate > 0 && n.fuel / n.fuelDrainRate < minFuelDrainTime) minFuelDrainTime = n.fuel / n.fuelDrainRate;
                    }

                    foreach (FuelNode n in nodes)
                    {
                        //print("" + n.part + " - fuel = " + n.fuel + "; fuelDrainRate = " + n.fuelDrainRate + "; time left = " + n.fuel / n.fuelDrainRate);
                    }
                    
                    //advance time until some fuel node is emptied
                    float dt = minFuelDrainTime;
                    print("t = " + t + "; dt = " + dt);
                    float startMass = totalShipMass();
                    foreach (FuelNode n in nodes)
                    {
                        n.fuel -= n.fuelDrainRate * dt;
                    }
                    float endMass = totalShipMass();
                    t += dt;

                    //calculate how much dV was produced during this time step
                    if (dt > 0 && startMass > endMass && startMass > 0 && endMass > 0)
                    {
                        stageDeltaV += totalStageThrust * dt / (startMass - endMass) * Mathf.Log(startMass / endMass);
                    }

                    //remove drained nodes from the fuel flow graph
                    killEmptySources(engines);

                    //check if we can stage yet
                    if (allowedToStage2()) doneStage = true;
                }

                stageTime = t;

                print("stage " + simStage + " time = " + stageTime + ", deltaV = " + stageDeltaV + "; final mass = " + totalShipMass());

                timePerStage[simStage] = stageTime;
                deltaVPerStage[simStage] = stageDeltaV;

                simStage--;
                simulateStageActivation();
            }

        }

        bool allowedToStage()
        {
            //currently we stage the simulation whenever an engine runs out of fuel, but this isn't right.
            //we should use the same set of critera as in the ascent AP
            List<FuelNode> engines = findActiveEngines(simStage);
            foreach (FuelNode engine in engines)
            {
                if (engine.sources.Count == 0)
                {
                    //                            print("" + engine.part + " ran out of fuel; staging");
                    return true;
                }
            }

            return false;
        }

        bool allowedToStage2()
        {
            //if no engines are active, we can always stage
            if (findActiveEngines(simStage).Count == 0) return true;

            //if staging would decouple an active engine or non-empty fuel tank, we're not allowed to stage
            foreach (FuelNode n in nodes)
            {
                if (n.decoupledInStage == (simStage - 1))
                {
                    if (n.fuel > 0 || (n.fuelConsumption > 0 && n.sources.Count > 0 && n.part.inverseStage >= simStage))
                    {
                        //                        print("can't stage because it would decouple " + n.part);
                        return false;
                    }
                }
            }

            //if this isn't the last stage, we're allowed to stage
            if (simStage > 0) return true;

            //if this is the last stage, we're not allowed to stage (finish) unless there are no active engines remaining
            foreach (FuelNode n in nodes)
            {
                if (n.fuelConsumption > 0 && n.sources.Count > 0) return false;
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
                killSource(n);
                nodes.Remove(n);
            }
        }

        float totalShipMass()
        {
            float ret = 0;
            foreach (FuelNode node in nodes) ret += node.mass;
            return ret;
        }

        List<FuelNode> findActiveEngines(int stage)
        {
            List<FuelNode> engines = new List<FuelNode>();
            foreach (FuelNode node in nodes)
            {
                if (node.fuelConsumption > 0 && node.part.inverseStage >= stage)
                {
                    engines.Add(node);
                }
            }

            return engines;
        }

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
                        if(killSource(n)) sourceKilled = true;
                    }
                }
            }
        }

        void assignFuelDrainRates()
        {
            foreach (FuelNode n in nodes) n.fuelDrainRate = 0;

            List<FuelNode> engines = findActiveEngines(simStage);

            foreach (FuelNode engine in engines) engine.assignFuelDrainRates();
        }

        bool killSource(FuelNode source)
        {
            source.fuel = 0;

            foreach (FuelNode n in nodes)
            {
                if (n.sources.Contains(source))
                {
                    n.sources.Remove(source);
                    return true;
                }
            }

            return false;
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

        public FuelNode(Part part)
        {
            this.part = part;

            if (part is FuelTank)
            {
                fuel = ((FuelTank)part).fuel;
            }

            if (part is SolidRocket)
            {
                fuel = ((SolidRocket)part).internalFuel;
//                MonoBehaviour.print("creating solid rocket node with fuel = " + fuel);
                thrust = ((SolidRocket)part).thrust;
                fuelConsumption = ((SolidRocket)part).fuelConsumption;
            }

            if (part is LiquidEngine)
            {
                thrust = ((LiquidEngine)part).maxThrust;
                fuelConsumption = ((LiquidEngine)part).fuelConsumption;
            }

            if (part is LiquidFuelEngine)
            {
                thrust = ((LiquidFuelEngine)part).maxThrust;
                fuelConsumption = ((LiquidFuelEngine)part).fuelConsumption;
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
                else
                {
                    p = p.parent;
                }
            }
        }

        public float mass
        {
            get
            {
                if (part.physicalSignificance == Part.PhysicalSignificance.NONE) return 0.0F;

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
                this.assignFuelDrainRates(this.fuelConsumption, new List<FuelNode>());
            }
        }

        void assignFuelDrainRates(float totalDrainRate, List<FuelNode> visited)
        {
            List<FuelNode> newVisited = new List<FuelNode>(visited);
            newVisited.Add(this);

            //first seee if we can drain fuel through fuel lines
            List<FuelNode> fuelLines = new List<FuelNode>();
            foreach (FuelNode n in sources)
            {
                if (n.part is FuelLine && !visited.Contains(n)) fuelLines.Add(n);
            }
            if (fuelLines.Count > 0)
            {
                foreach (FuelNode fuelLine in fuelLines)
                {
                    fuelLine.assignFuelDrainRates(totalDrainRate / fuelLines.Count, newVisited);
                }
                return;
            }

            //if there are no incoming fuel lines, try other sources
            foreach (FuelNode n in sources)
            {
                if (!visited.Contains(n))
                {
                    if (drainFromSourceBeforeSelf(n))
                    {
                        n.assignFuelDrainRates(totalDrainRate, newVisited);
                        return;
                    }
                }
            }

            //in the final extremity, drain fuel from this part
            fuelDrainRate += totalDrainRate;
        }

        //if we still have fuel, don't drain through the parent unless the parent node is a stack node
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
