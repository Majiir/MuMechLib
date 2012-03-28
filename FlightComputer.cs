using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;


//An interface for things that control the ship or display information about them.
//FlightComputers implement many of the same methods as Part so that they can respond
//to the relevant events. They also implement drive() and drawGUI(), which are only 
//called when the FlightComputer is enabled. drive() does fly-by-wire and drawGUI() 
//displays the FlightComputer's GUI.
//ARAutopilot manages several FlightComputers
/*
interface FlightComputer
{
    bool IsEnabled { get; set; }

    void onPartStart();

    void onFlightStart();

    void onFlightStartAtLaunchPad();

    void onPartFixedUpdate();


    void drive(FlightCtrlState s);

    void drawGUI();


    String name();
}*/

