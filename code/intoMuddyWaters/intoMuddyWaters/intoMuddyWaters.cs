using System;
using System.Windows.Forms;
using System.Drawing;
using GTA;
using GTA.Math;
using GTA.Native;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace intoMuddyWaters
{

    // Sound player
    public class SoundEffect
    {
        string _soundFile;
        Thread _soundThread;
        bool _isStopped = true;
        public bool IsFinished { get { return _isStopped; } }

        public SoundEffect(string soundFile)
        {
            _soundFile = soundFile;
        }

        public void Play()
        {
            if (!_isStopped)
                return;

            _soundThread = new Thread(PlayThread);
            _soundThread.Start();
        }

        private void PlayThread()
        {
            _isStopped = false;
            System.Media.SoundPlayer player = new System.Media.SoundPlayer(_soundFile);
            player.PlaySync();
            _isStopped = true;
        }
    }

    // Script
    public class intoMuddyWaters : Script
    {

        bool modON; // Mod on/off
        bool init; // Was mod already initiated?

        List<string> titles; // List of tite strings
        bool titleON; // Activate titles
        int titleTimer; // Timer for title screens
        int frameIndex; // An index to count the frames for the deer spawn
        int sceneNr; // Number of scenes
        int sceneIndex; // Active scene

        SoundEffect audioEssay; // A soundplayer to play each of the files
        bool isFilePlaying = false; //check if the sound file is playing

        List<Action> scenes;

        Ped player; // Player ped
        Vector3 playerPos; // Position of player ped

        Vector3 spawnPos;
        Vector3 centre; // Centre point of swamp area
        float maxDistance; // Radius around swamp area

        bool deerSpawned = false; // Check if deer have been spawned already in epilogue
        int deerNR = 3; // How many deer?
        List<Ped> deer = new List<Ped>(); // A school of deer

        int POV;
        TimeSpan time;
        Weather weather;
        bool hasRifle;
        int rifleAmmo;
        bool effectAct; // State of the visual effect


        // This stuff is to control keyboard and mouse //////////////////////////////////////////////////////////////////////////////////////////
        [StructLayout(LayoutKind.Sequential)]
        public struct KeyboardInput
        {
            public ushort wVk;
            public ushort wScan;
            public uint dwFlags;
            public uint time;
            public IntPtr dwExtraInfo;
        }
        [StructLayout(LayoutKind.Sequential)]
        public struct MouseInput
        {
            public int dx;
            public int dy;
            public uint mouseData;
            public uint dwFlags;
            public uint time;
            public IntPtr dwExtraInfo;
        }
        [StructLayout(LayoutKind.Sequential)]
        public struct HardwareInput
        {
            public uint uMsg;
            public ushort wParamL;
            public ushort wParamH;
        }
        [StructLayout(LayoutKind.Explicit)]
        public struct InputUnion
        {
            [FieldOffset(0)] public MouseInput mi;
            [FieldOffset(0)] public KeyboardInput ki;
            [FieldOffset(0)] public HardwareInput hi;
        }
        public struct Input
        {
            public int type;
            public InputUnion u;
        }
        [Flags]
        public enum InputType
        {
            Mouse = 0,
            Keyboard = 1,
            Hardware = 2
        }
        [Flags]
        public enum KeyEventF
        {
            KeyDown = 0x0000,
            ExtendedKey = 0x0001,
            KeyUp = 0x0002,
            Unicode = 0x0004,
            Scancode = 0x0008
        }
        [Flags]
        public enum MouseEventF
        {
            Absolute = 0x8000,
            HWheel = 0x01000,
            Move = 0x0001,
            MoveNoCoalesce = 0x2000,
            LeftDown = 0x0002,
            LeftUp = 0x0004,
            RightDown = 0x0008,
            RightUp = 0x0010,
            MiddleDown = 0x0020,
            MiddleUp = 0x0040,
            VirtualDesk = 0x4000,
            Wheel = 0x0800,
            XDown = 0x0080,
            XUp = 0x0100
        }
        [DllImport("user32.dll", SetLastError = true)]
        private static extern uint SendInput(uint nInputs, Input[] pInputs, int cbSize);
        [DllImport("user32.dll")]
        private static extern IntPtr GetMessageExtraInfo();
        bool pressKey = false;
        ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

        public intoMuddyWaters()
        {
            this.Tick += onTick;
            this.KeyUp += onKeyUp;


            modON = false;
            init = false;

            titles = new List<string> {
                "INTO MUDDY WATERS",
                "THE SWAMP IS DARK AND FRIGHTENING",
                "THE SWAMP IS UNKNOWABLE TO MOST",
                "THE SWAMP ABIDES THE OUTCASTS",
                "THE SWAMP IS PREGNANT WITH DECAY",
                "A MONSTER OF ONE'S OWN MAKING"
            };
            sceneNr = titles.Count;
            sceneIndex = 0;
            titleON = false;

            scenes = new List<Action>
        {
            intro, chapter1, chapter2, chapter3, chapter4, epilogue, reset
        };


            spawnPos = new Vector3(-2099f, 2636f, 3f);
            centre = new Vector3(-2030f, 2600f, 1.5f);
            maxDistance = 200f;

            effectAct = false;

        }

        private void onTick(object sender, EventArgs e)
        {



            // Get player position
            player = Game.Player.Character;
            playerPos = player.Position;

            // When mod is ON
            if (modON)
            {

                // Initiate: Teleport and activate first sound file once
                if (!init)
                {

                    // Get current stats
                    // POV
                    POV = Function.Call<int>(Hash.GET_FOLLOW_PED_CAM_VIEW_MODE);
                    Function.Call(Hash.SET_CURRENT_PED_WEAPON, player.Handle, (uint)WeaponHash.Unarmed, true);
                    // Weather
                    weather = World.Weather;
                    // Time
                    time = World.CurrentTimeOfDay;
                    // Rifle
                    hasRifle = Function.Call<bool>(Hash.HAS_PED_GOT_WEAPON, player.Handle, (uint)WeaponHash.SniperRifle, false);
                    rifleAmmo = Function.Call<int>(Hash.GET_AMMO_IN_PED_WEAPON, player.Handle, (uint)WeaponHash.SniperRifle);


                    Game.Player.Character.Position = spawnPos;
                    init = true;

                    audioEssay = new SoundEffect("./scripts/audioEssay/_intro.wav");//load a new dialogue
                    audioEssay.Play();
                    isFilePlaying = true;
                    titleTimer = Game.GameTime;
                }


                // Enforce barrier
                float distance = playerPos.DistanceTo(centre);
                //GTA.UI.Screen.ShowHelpText("distance: " + distance, 1000, false);
                // Force player back if they reach barrier
                if (distance > maxDistance)
                {
                    //Function.Call(Hash.SET_PLAYER_CONTROL, Game.Player, false, 0);
                    Vector3 direction = (centre - playerPos);
                    direction.Z = 0f;
                    direction.Normalize();
                    float forceStrength = 2f+sceneIndex;
                    player.ApplyForce(direction * forceStrength);
                }

                //else Function.Call(Hash.SET_PLAYER_CONTROL, Game.Player, true, 0);

                // Make player invincible
                player.IsInvincible = true;
                // If the player has any wanted level, clear it
                if (Game.Player.WantedLevel > 0)
                {
                    Game.Player.WantedLevel = 0;
                }

                // Activate scenes
                for (int i = 0; i <= sceneIndex; i++)
                {
                    scenes[i]();

                    // [0] Activate intro
                    // 1st person

                    // [1] Activate chapter 1
                    // Time and weather

                    // [2] Activate chapter 2
                    // Effect

                    // [3] Activate chapter 3
                    // Scope view

                    // [4] Activate chapter 4
                    // Night vision

                    // [5] Activate epilogue
                    // Termal vision and deers, deers, deers

                    // [6] Back to normal
                }

                //if audio file is finished, play next chapter audio file
                if (isFilePlaying && audioEssay.IsFinished == true)
                {
                    isFilePlaying = false;
                    sceneIndex++;

                    if (sceneIndex < sceneNr)
                    {
                        audioEssay = new SoundEffect("./scripts/audioEssay/" + scenes[sceneIndex].Method.Name + ".wav");//load a new dialogue
                        audioEssay.Play();
                        isFilePlaying = true;
                        titleTimer = Game.GameTime;

                        // Clear vehicles in the area
                        foreach (Vehicle veh in World.GetNearbyVehicles(centre, 100f))
                        {
                            if (veh.Exists())
                            {
                                veh.Delete();
                            }
                        }
                    }
                }

                if (Game.GameTime - titleTimer < 4000) { titleON = true; }
                else { titleON = false; }

                // Draw title
                if (titleON)
                {
                    ShowText(0.5f, 0.45f, titles[sceneIndex], 1.2f, 0);
                }


            }

        }

        private void onKeyUp(object sender, KeyEventArgs e)
        {

            if (e.KeyCode == Keys.H)
            {
                modON = true;
            }
            if (e.KeyCode == Keys.J)
            {

                player.Weapons.Remove(WeaponHash.SniperRifle);
                // Give the player the sniper rifle
                Function.Call(Hash.GIVE_WEAPON_TO_PED, player, (uint)WeaponHash.SniperRifle, 999, true, true);
                Function.Call(Hash.SET_PED_AMMO, player.Handle, (uint)WeaponHash.SniperRifle, 19);

                // Force equip
                Function.Call(Hash.SET_CURRENT_PED_WEAPON, player, (uint)WeaponHash.SniperRifle, true);
            }
            if (e.KeyCode == Keys.K)
            {
                int totalAmmo = Function.Call<int>(Hash.GET_AMMO_IN_PED_WEAPON, player.Handle, (uint)WeaponHash.SniperRifle);
                GTA.UI.Screen.ShowHelpText("Ammo: " + totalAmmo, 1000, false);
            }

        }


        private void ShowText(float x, float y, string text, float size, int font)
        {

            Function.Call(Hash.DRAW_RECT, 0f, 0f, 2f, 2f, 0, 0, 0, 255);
            Function.Call(Hash.SET_TEXT_FONT, font);
            Function.Call(Hash.SET_TEXT_SCALE, size, size);
            Function.Call(Hash.SET_TEXT_COLOUR, 255, 255, 255, 255);
            Function.Call(Hash.SET_TEXT_WRAP, 0.0, 1.0);
            Function.Call(Hash.SET_TEXT_CENTRE, 1);
            Function.Call(Hash.SET_TEXT_OUTLINE, true);
            Function.Call(Hash.BEGIN_TEXT_COMMAND_DISPLAY_TEXT, "CELL_EMAIL_BCON");
            Function.Call(Hash.ADD_TEXT_COMPONENT_SUBSTRING_PLAYER_NAME, text);
            Function.Call(Hash.END_TEXT_COMMAND_DISPLAY_TEXT, x, y);
        }

        public void intro()
        {

            // Force first-person view
            Function.Call(Hash.SET_FOLLOW_PED_CAM_VIEW_MODE, 4);
            World.CurrentTimeOfDay = new TimeSpan(0, 0, 0);
        }

        public void chapter1()
        {
            // Set weather and time
            World.Weather = Weather.ThunderStorm;
        }

        public void chapter2()
        {
            Function.Call(Hash.ANIMPOSTFX_PLAY, "PPFilterOut", 0, true);
            Function.Call(Hash.ANIMPOSTFX_PLAY, "CayoUndeadDJ", 0, true);
            Function.Call(Hash.ANIMPOSTFX_PLAY, "DeathFailMPDark", 0, true);
            Function.Call(Hash.ANIMPOSTFX_PLAY, "MP_TransformRaceFlash", 0, true);
            Function.Call(Hash.ANIMPOSTFX_PLAY, "PeyoteEndOut", 0, true);
            Function.Call(Hash.ANIMPOSTFX_PLAY, "RampageOut", 0, true);
            if (!effectAct)
            {
                // Start effect
                Function.Call(Hash.ANIMPOSTFX_PLAY, "DrugsTrevorClownsFight", 0, true);
                Function.Call(Hash.ANIMPOSTFX_PLAY, "SurvivalAlien", 0, true);

                effectAct = true;
            }
        }

        public void chapter3()
        {

            // Give the player the sniper rifle
            Function.Call(Hash.GIVE_WEAPON_TO_PED, player, (uint)WeaponHash.SniperRifle, 999, false, true);

            // Force equip
            Function.Call(Hash.SET_CURRENT_PED_WEAPON, player, (uint)WeaponHash.SniperRifle, true);

            // Force scope view
            mouseRightDown();
        }

        public void chapter4()
        {
            Function.Call(Hash.SET_NIGHTVISION, true);
        }

        public void epilogue()
        {
            Function.Call(Hash.SET_SEETHROUGH, true);

            // deers, deers, deers
            if (!deerSpawned)
            {
                // Load the model
                Model deerModel = new Model(PedHash.Deer);
                deerModel.Request();
                while (!deerModel.IsLoaded)
                {
                    Script.Wait(50);
                }

                if (titleON == false)
                {
                    // Spawn a school of deer
                    for (int i = 0; i < 100; i++)
                    {

                        Random rand = new Random();

                        // Random angle and distance (for a circular area)
                        double angle = rand.NextDouble() * Math.PI * 2;
                        double distance = rand.NextDouble() * maxDistance;

                        // Offset from the center
                        float offsetX = (float)(Math.Cos(angle) * distance);
                        float offsetY = (float)(Math.Sin(angle) * distance);


                        // Calculate position
                        Vector3 spawnPos = new Vector3(centre.X + offsetX, centre.Y + offsetY, centre.Z + 100f);

                        // Find ground height/////only for SHVDN3.7 and newer
                        /*float groundZ;
                        if (World.GetGroundHeight(spawnPos, out groundZ))
                        {
                            spawnPos.Z = groundZ;
                        }*/
                        // Find ground height////////for SHVDN3
                        float groundZ = World.GetGroundHeight(new Vector2(spawnPos.X, spawnPos.Y));
                        if (groundZ > 0.0f)
                        {
                            spawnPos.Z = groundZ + 0.1f;
                        }

                        // Spawn the deer
                        Ped newDeer = World.CreatePed(deerModel, spawnPos);
                        // Configure the deer so that they don't get scared and start a run away animation
                        newDeer.BlockPermanentEvents = true;
                        newDeer.AlwaysKeepTask = true;
                        newDeer.CanBeTargetted = false;
                        newDeer.CanWrithe = false;
                        newDeer.IsInvincible = true;

                        Function.Call(Hash.SET_PED_FLEE_ATTRIBUTES, newDeer, 0, false);
                        Function.Call(Hash.SET_PED_HEARING_RANGE, newDeer, 0.0f);
                        Function.Call(Hash.SET_PED_SEEING_RANGE, newDeer, 0.0f);
                        Function.Call(Hash.SET_BLOCKING_OF_NON_TEMPORARY_EVENTS, newDeer, true);

                        Function.Call(Hash.SET_PED_CONFIG_FLAG, newDeer, 117, false); //CPED_CONFIG_FLAG_BumpedByPlayer = 117,
                        Function.Call(Hash.SET_PED_CONFIG_FLAG, newDeer, 128, false); //CPED_CONFIG_FLAG_CanBeAgitated = 128,
                        Function.Call(Hash.SET_PED_CONFIG_FLAG, newDeer, 183, false); //CPED_CONFIG_FLAG_IsAgitated = 183,
                        newDeer.Task.ClearAllImmediately();
                        newDeer.Task.StartScenario("WORLD_HUMAN_PUSH_UPS", newDeer.Position, 0);
                        Function.Call(Hash.SET_PED_KEEP_TASK, newDeer, true);

                        deer.Add(newDeer);
                        Wait(100);
                    }
                    deerSpawned = true;
                }


                // Make deer doing push ups every frame
                if (deer.Count > 0 && deerSpawned)
                {
                    for (int i = 0; i < deer.Count; i++)
                    {

                        if ((Function.Call<bool>(Hash.IS_PED_USING_ANY_SCENARIO, deer[i])) == false) //(Function.Call<bool>(Hash.GET_IS_TASK_ACTIVE, deer[i], 118) == false)
                        {
                            deer[i].Task.ClearAllImmediately();
                            deer[i].Task.StartScenario("WORLD_HUMAN_PUSH_UPS", deer[i].Position, 0);
                            Function.Call(Hash.SET_PED_KEEP_TASK, deer[i], true);
                            Wait(100);
                        }
                    }
                }
            }
        }

        public void reset()
        {

            // Undo initialization
            init = false;
            player.IsInvincible = false;

            // Undo epilogue: first-person view
            Function.Call(Hash.SET_FOLLOW_PED_CAM_VIEW_MODE, POV);

            // Undo chapter 1: weather and time
            World.Weather = weather;
            World.CurrentTimeOfDay = time;

            // Undo chapter 2: release zoom, reset rifle
            mouseRightUp();
            player.Weapons.Remove(WeaponHash.SniperRifle);
            if (hasRifle)
            {
                player.Weapons.Give(WeaponHash.SniperRifle, 999, true, true);
                Function.Call(Hash.SET_PED_AMMO, player.Handle, (uint)WeaponHash.SniperRifle, rifleAmmo);
            }
            Function.Call(Hash.SET_CURRENT_PED_WEAPON, player.Handle, (uint)WeaponHash.Unarmed, true);

            // Undo chapter 3: end visual effect
            Function.Call(Hash.ANIMPOSTFX_STOP_ALL);
            effectAct = false;

            // Undo chapter 4: no night vision
            Function.Call(Hash.SET_NIGHTVISION, false);

            // Undo epilogue: no thermal vision and delete deers
            Function.Call(Hash.SET_SEETHROUGH, false);

            for (int i = 0; i < deer.Count; i++)
            {
                deer[i].Delete();
            }
            deerSpawned = false;

            // Turn mod off
            modON = false;

            // Reset scene index
            sceneIndex = 0;
        }

        private void OnAborted(object sender, EventArgs e)
        {
            //release forced mouse click
            mouseRightUp();
        }

        public static void mouseRightDown()
        {
            //mouse press
            Input[] inputs = new Input[]
            {
                new Input
                {
                    type = (int) InputType.Mouse,
                    u = new InputUnion
                    {
                        mi = new MouseInput
                        {
                            dwFlags = (uint)(MouseEventF.RightDown),
                            dwExtraInfo = GetMessageExtraInfo()
                        }
                    }
                }
            };
            SendInput((uint)inputs.Length, inputs, Marshal.SizeOf(typeof(Input)));
        }

        public static void mouseRightUp()
        {
            //mouse release
            Input[] inputs = new Input[]
            {
                new Input
                {
                    type = (int) InputType.Mouse,
                    u = new InputUnion
                    {
                        mi = new MouseInput
                        {
                            dwFlags = (uint)(MouseEventF.RightUp),
                            dwExtraInfo = GetMessageExtraInfo()
                        }
                    }
                }
            };
            SendInput((uint)inputs.Length, inputs, Marshal.SizeOf(typeof(Input)));
        }

    }
}