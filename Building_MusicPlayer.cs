/*
 * Created by SharpDevelop.
 * User: white_altar
 * Date: 2/26/2017
 * Time: 4:04 PM
 * 
 * To change this template use Tools | Options | Coding | Edit Standard Headers.
 */

 
 // ----------------------------------------------------------------------
// These are basic usings. Always let them be here.
// ----------------------------------------------------------------------
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

// ----------------------------------------------------------------------
// These are RimWorld-specific usings. Activate/Deactivate what you need:
// [If the usings are not found, (red line under it) fix in References]
// ----------------------------------------------------------------------
using UnityEngine;         // Always needed
//using VerseBase;         // Material/Graphics handling functions are found here
using Verse;               // RimWorld universal objects are here (like 'Building')
//using Verse.AI;          // Needed when you do something with the AI
using Verse.Sound;         // Needed when you do something with Sound
//using Verse.Noise;       // Needed when you do something with Noises
using RimWorld;            // RimWorld specific functions are found here (like 'Building_Battery')
//using RimWorld.Planet;   // RimWorld specific functions for world creation
//using RimWorld.SquadAI;  // RimWorld specific functions for squad brains 


// This is the namespace of your mod. Change it to your liking, but don't forget to change it in your XML too.
namespace MusicJoy
{
    /// <summary>
    /// Description of Building_MusicPlayer: This building object class should initiate the playing of a "song file" via a "SongDef" when a 
    /// particular set of criteria have been met.
    /// </summary>
    public class Building_MusicPlayer : Building, IBillGiver, IBillGiverWithTickAction
    {
        // ===================== Variables =====================
        // Work variable
        private int counter = 0;                  // 60Ticks = 1s // 20000Ticks = 1 Day
        private Phase phase = Phase.off;          // Actual phase
        private Phase phaseOld = Phase.online;    // Save-variable

        // Variables to set a specific value
        private const int counterPhasePlayingMax = 216000;  // Playing-Time
        private const int counterPhaseOnlineMax = 2000000;  // Online-Time
        private float powerInputPlaying = -250;             // Power needed at Playing
        private float powerInputOnline = 0;                 // Power needed at Active

        // Work enumeration - To make the reading of the active phase easier
        private enum Phase
        {
            off = 0,
            playing,
            online
        }

        // Component references (will be set in 'SpawnSetup()')
        // CompPowerTrader  - Checks, if power is available; takes power from the powernet;...
        private CompPowerTrader powerComp;
        private BillStack billStack;
        private CompRefuelable refuelableComp;
        private CompBreakdownable breakdownableComp;

        // Sound references
        private static readonly SoundDef SoundHiss = SoundDef.Named("PowerOn");

        // Text-variables: with .Translate() they are updated from the language file in the folder 'Languages\en_US\Keyed\...' with the active language.
        // Look into the function 'GetInspectString()' on how it will be translated
        private string txtStatus = "Player Status: ";
        private string txtOff = " Off";
        private string txtPlaying = " Playing";
        private string txtOnline = " Online";

        // Destroyed flag. Most of the time not really needed, but sometimes...
        private bool destroyedFlag = false;


//Define inheritance to be like another object
        protected Building_WorkTable SelTable
        {
            get
            {
                return (Building_WorkTable)base.SelThing;
            }
        }


        // ===================== Setup Work =====================
        // --- Not really needed here ---
        ///// <summary>
        ///// Do something after the object is initialized, but before it is spawned
        ///// </summary>
        //public override void PostMake()
        //{
        //    // Do the work of the base class (Building)
        //    base.PostMake();
        //}


        /// <summary>
        /// Tell system that this player can work without power 
        /// (handCrank refuelable only), or that it can't
        /// </summary>
        public bool CanWorkWithoutPower
        {
            get
            {
                return this.powerComp == null || this.def.building.unpoweredWorkTableWorkSpeedFactor > 0f;
            }
        }


        /// <summary>
        /// Tell system that this player can be used now if it CanWorkWithoutPower
        /// or is powered, AND is fueled or doesn't need it, AND isn't broken
        /// </summary>
        public virtual bool UsableNow
        {
            get
            {
                return (this.CanWorkWithoutPower || (this.powerComp != null && this.powerComp.PowerOn)) && (this.refuelableComp == null || this.refuelableComp.HasFuel) && (this.breakdownableComp == null || !this.breakdownableComp.BrokenDown);
            }
        }


        /// <summary>
        /// Tell system that this player uses the BillStack system
        /// </summary>
        public BillStack BillStack
        {
            get
            {
                return this.billStack;
            }
        }

        public IntVec3 BillInteractionCell
        {
            get
            {
                return this.InteractionCell;
            }
        }

        public IEnumerable<IntVec3> IngredientStackCells
        {
            get
            {
                return GenAdj.CellsOccupiedBy(this);
            }
        }

        /// <summary>
        /// Tying to get class read by program XML
        /// </summary>
        public EditorShowClassNameAttribute getClass
        {
            get
            {
                return (EditorShowClassNameAttribute)this;
            }
        }

        public static explicit operator EditorShowClassNameAttribute(Building_MusicPlayer v)
        {
            throw new NotImplementedException();
        }


        ///public virtual Map get_Map()
        ///{
        ///    return base.Map;
        ///}


        /// <summary>
        /// Assign the BillStack to this MusicPlayer
        /// </summary>
        public Building_MusicPlayer()
        {
			this.billStack = new BillStack(this);
        }

		
        /// <summary>
        /// Do something after the object is spawned
        /// </summary>
        public override void SpawnSetup(Map map)
        {
            // Do the work of the base class (Building) and check fuel (cranks) and repair status
            base.SpawnSetup(map);
            this.billStack = new BillStack(this);
            this.refuelableComp = base.GetComp<CompRefuelable>();
			this.breakdownableComp = base.GetComp<CompBreakdownable>();
            // Get references to the components CompPowerTrader
            SetPower();
        }


        /// <summary>
        /// To save and load actual values (savegame-data) 
        /// </summary>
        public override void ExposeData()
		{
			base.ExposeData();
			// Save and load the work variables, so they don't default after loading
            Scribe_Values.LookValue<Phase>(ref phase, "phase", Phase.off);
            Scribe_Values.LookValue<int>(ref counter, "counter", 0);
			Scribe_Deep.LookDeep<BillStack>(ref this.billStack, "billStack", new object[]
			{
				this
			});

		    // Set the old value to the phase value
            phaseOld = phase;
            
            // Get references to the components CompPowerTrade
            SetPower();
        }
        

        /// <summary>
        /// Find the PowerComp
        /// </summary>
        private void SetPower()
        {
            // Get references to the components CompPowerTrader
            powerComp = base.GetComp<CompPowerTrader>();

            // Preset the PowerOutput to 0 (negative values will draw power from the powernet)
            powerComp.PowerOutput = 0;
        }
        

        // ===================== Destroy =====================
        /// <summary>
        /// Clean up when this is destroyed
        /// </summary>
        public override void Destroy(DestroyMode mode = DestroyMode.Vanish)
        {
            // block further ticker work
            destroyedFlag = true;

            base.Destroy(mode);
        }

        // ===================== Ticker =====================
        /// <summary>
        /// This is used, when the Ticker in the XML is set to 'Rare'
        /// This is a tick thats done once every 250 normal Ticks
        /// </summary>
        public override void TickRare()
        {
            if (destroyedFlag) // Do nothing further, when destroyed (just a safety)
                return;

            // Don't forget the base work
            base.TickRare();

            // Call work function
            DoTickerWork(250);
        }


        /// <summary>
        /// This is used, when the Ticker in the XML is set to 'Normal'
        /// This Tick is done often (60 times per second)
        /// </summary>
        public override void Tick()
        {
            if (destroyedFlag) // Do nothing further, when destroyed (just a safety)
                return;

            base.Tick();

            // Call work function
            DoTickerWork(1);
        }

        
        /// <summary>
        /// If the player is refuelable (crankable), this tells it it used up a tick.
        /// </summary>
		public virtual void UsedThisTick()
		{
			if (this.refuelableComp != null)
			{
				this.refuelableComp.Notify_UsedThisTick();
			}
		}
        
        
		public bool CurrentlyUsable()
		{
			return this.UsableNow;
		}

		
        // ===================== Main Work Function =====================

        /// <summary>
        /// This will be called from one of the Ticker-Functions.
        /// </summary>
        /// <param name="tickerAmount"></param>
        private void DoTickerWork(int tickerAmount)
        {
            // The following, if activated, creates an entry to the output_log.txt file, so that you can debug something
            // Log.Error("This description will be shown, if active, in the console and always in the output_log.txt");             !!!!

            if (powerComp.PowerOn)
            {
                // Power is on -> do work
                // ----------------------

                // We have 3 Phases: Off, Playing, Online
                // Off: When the power is cut, it is off (counter will be reset)
                // Play: For 1 hour per (ten minutes of) crank refueling, it will play bills (recordings)
                // Online: It is ready to play a recording and (if possible) receiving communications


                // phase == off (status after power switch off)
                if (phase == Phase.off)
                {
                    // Savety to prevent a loop if old == off
                    if (phaseOld == Phase.off)
                        phaseOld = Phase.online;

                    // set to the old phase
                    phase = phaseOld;
                    return;
                }

                // set the old variable
                phaseOld = phase;

                // increase the counter by the ticker amount
                counter += tickerAmount; // +1 with normal ticker, +250 with rare ticker

                // phase == playing
                if (phase == Phase.playing)
                {
                    if (counter >= counterPhasePlayingMax) // counter >= 216000 ?
                    {
                        // Switch to off, counter 0
                        phase = Phase.off;
                        counter = 0;
                        return;
                    }
                    powerComp.PowerOutput = powerInputPlaying; // value: -250
                }

                // phase == online
                if (phase == Phase.online)
                {
                    if (counter >= counterPhaseOnlineMax) // counter >= 2000000 ?
                    {
                        // Switch to off, counter 0
                        phase = Phase.off;
                        counter = 0;
                        return;
                    }
                    powerComp.PowerOutput = powerInputOnline; // value: -1
                }

            }
            else
            {
                // Power off

                // save old phase
                if (phase != Phase.off)
                    phaseOld = phase;

                // set phase to off
                phase = Phase.off;

                powerComp.PowerOutput = 0;
            }
        }


        // ===================== Inspections =====================

        /// <summary>
        /// This string will be shown when the object is selected (focus)
        /// </summary>
        /// <returns></returns>
        public override string GetInspectString()
        {
            StringBuilder stringBuilder = new StringBuilder();

            // Add the inspections string from the base
            stringBuilder.Append(base.GetInspectString());

            // Add your own strings (caution: string shouldn't be more than 5 lines (including base)!)
            //stringBuilder.Append("Now Playing: " + songDef.Label + " at -0.8 dB");
            //stringBuilder.AppendLine();
            stringBuilder.AppendLine();
            stringBuilder.Append(txtStatus.Translate() + " ");  // <= TRANSLATION

            // Phase -> Offline: Add text 'Off' (Translation from active language file)
            if (phase == Phase.off)
                stringBuilder.Append(txtOff.Translate());   // <= TRANSLATION

            // Phase -> Recharging: Add text 'Playing' (Translation from active language file)
            if (phase == Phase.playing)
                stringBuilder.Append(txtPlaying.Translate());     // <= TRANSLATION

            // Phase -> Active: Add text 'Online' (Translation from active language file)
            if (phase == Phase.online)
                stringBuilder.Append(txtOnline.Translate());    // <= TRANSLATION

            // return the complete string
            return stringBuilder.ToString();
        }



        ///// <summary>
        ///// This creates selection buttons
        ///// </summary>
        ///// <returns></returns>
        //public override IEnumerable<Command> GetGizmos()
        //{
        //    IList<Command> list = new List<Command>();

        //    // Key-Binding F - 
        //    Command_Action optF;
        //    optF = new Command_Action();
        //    optF.icon = UI_DoorLocked;
        //    optF.defaultDesc = txtLocksUnlocksDoor.Translate();
        //    optF.hotKey = KeyCode.F;
        //    optF.activateSound = SoundDef.Named("Click");
        //    optF.action = DoWorkFunction;
        //    optF.groupKey = 1234567; // unique number, for grouping in game
        //    // yield return optF;
        //    list.Add(optF);

        //    // Adding the base.GetCommands() when not empty
        //    IEnumerable<Command> baseList = base.GetGizmos();
        //    if (baseList != null)
        //        return list.AsEnumerable<Command>().Concat(baseList);
        //    else
        //        return list.AsEnumerable<Command>();
        //}



    }
}