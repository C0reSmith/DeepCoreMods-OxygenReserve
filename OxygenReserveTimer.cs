using System;
using System.Threading;
using Assets.Scripts.Objects.Entities;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using UnityEngine;


namespace DeepCoreMods.OxygenReserve
{
    internal static class OxygenReserveTimer
    {
        // --------------------------------------------------------------
        // Timing
        // --------------------------------------------------------------

        private const int InitialWorldLoadDelayMs = 15000;
        private const int OxygenCheckIntervalMs = 5000;
        private const int InputPollIntervalMs = 50;

        // Never allow an automatic refill above 90% of the
        // canister's structural maximum pressure.
        private const double MaxSafePressurePercent = 0.90;
        

        // --------------------------------------------------------------
        // Worker Thread
        // --------------------------------------------------------------

        private static Thread _workerThread;
        private static volatile bool _running;
        private static double _highestOxygenMoles = 0.0;
        private static bool _hasTrustedOxygenReference = false;
        private static bool _referenceLearningRequested = false;
        private static long _currentCanisterReferenceId = 0;
        private static bool _learnKeyWasDown;
        [DllImport("user32.dll")]
        private static extern short GetAsyncKeyState(int vKey);
        private static readonly Dictionary<long, double> _canisterOxygenReferences =
        new Dictionary<long, double>();
       
        // --------------------------------------------------------------
        // Persistence
        // --------------------------------------------------------------

        private static readonly string CanisterReferenceFile =
            Path.Combine(
                BepInEx.Paths.ConfigPath,
                "DeepCoreMods.OxygenReserve.canisters.txt");
        
        // --------------------------------------------------------------
        // Start
        // --------------------------------------------------------------

        public static void Start()
        {
            if (_running)
            {
                return;
            }

            _running = true;

            _workerThread = new Thread(WorkerLoop)
            {
                IsBackground = true,
                Name = "DeepCoreMods.OxygenReserve.Worker"
            };

            _workerThread.Start();

            Plugin.Log.LogInfo(
                "Oxygen Reserve worker thread started.");
        }

        // --------------------------------------------------------------
        // Worker
        // --------------------------------------------------------------

        private static void WorkerLoop()
        {
            Plugin.Log.LogInfo(
                "Oxygen Reserve worker thread is running.");
            // Restore any canister references saved during previous game sessions.
            LoadCanisterReferences();

            // Give Stationeers time to finish loading the world.
            Thread.Sleep(InitialWorldLoadDelayMs);

            DateTime nextOxygenCheck =
     DateTime.UtcNow;

            while (_running)
            {
                try
                {
                    // --------------------------------------------------
                    // Physical reference-learning key
                    // --------------------------------------------------

                    CheckLearnReferenceKey();

                    // --------------------------------------------------
                    // Oxygen tank check
                    // --------------------------------------------------

                    if (DateTime.UtcNow >= nextOxygenCheck)
                    {
                        nextOxygenCheck =
                            DateTime.UtcNow.AddMilliseconds(
                                OxygenCheckIntervalMs);

                        CheckAirTank();
                    }
                }
                catch (Exception ex)
                {
                    Plugin.Log.LogError(
                        $"Oxygen Reserve worker error: {ex}");
                }

                Thread.Sleep(InputPollIntervalMs);
            }

            Plugin.Log.LogInfo(
                "Oxygen Reserve worker thread stopped.");
        }

        // --------------------------------------------------------------
        // Air Tank Check
        // --------------------------------------------------------------

        private static void CheckAirTank()
        {
            var human = Human.LocalHuman;

            if (human == null)
            {
                Plugin.Log.LogInfo(
                    "Oxygen Reserve: Local human not available.");

                return;
            }

            var suit = human.Suit;

            if (suit == null)
            {
                Plugin.Log.LogInfo(
                    "Oxygen Reserve: No suit equipped.");

                return;
            }

            var airTank = suit.AirTank;

            if (airTank == null)
            {
                // Only log when a previously installed canister is removed.
                if (_currentCanisterReferenceId != 0)
                {
                    Plugin.Log.LogInfo(
                        "Oxygen Reserve: Air tank removed.");
                }

                // Clear the currently selected canister state.
                _currentCanisterReferenceId = 0;
                _highestOxygenMoles = 0.0;
                _hasTrustedOxygenReference = false;
                _referenceLearningRequested = false;

                return;
            }

            // ----------------------------------------------------------
            // Canister identification
            // ----------------------------------------------------------

            long canisterReferenceId = airTank.ReferenceId;

            if (_currentCanisterReferenceId != canisterReferenceId)
            {
                Plugin.Log.LogInfo(
                    $"Oxygen Reserve: Canister changed. " +
                    $"Previous ReferenceId = {_currentCanisterReferenceId} | " +
                    $"New ReferenceId = {canisterReferenceId}");

                _currentCanisterReferenceId = canisterReferenceId;

                // Check whether we have already learned this physical canister.
                if (_canisterOxygenReferences.TryGetValue(
                    canisterReferenceId,
                    out double savedOxygenReference))
                {
                    _highestOxygenMoles = savedOxygenReference;
                    _hasTrustedOxygenReference = true;

                    Plugin.Log.LogInfo(
                        $"Oxygen Reserve: Restored canister reference = " +
                        $"{_highestOxygenMoles:F4} mol");
                }
                else
                {
                    // New physical canister.
                    // Never carry another canister's reference across.
                    _highestOxygenMoles = 0.0;
                    _hasTrustedOxygenReference = false;

             Plugin.Log.LogInfo(
                     "Oxygen Reserve: New canister detected — " +
                     "trusted reference required.");

                }
            }
                       
            double pressureKPa = airTank.Pressure.ToDouble();
            
            double maxPressureKPa = airTank.MaxPressure.ToDouble();

            

            double maxSafePressureKPa =
                maxPressureKPa * MaxSafePressurePercent;

            double oxygenMoles =
                airTank.InternalAtmosphere
                    .GasMixture
                    .Oxygen
                    .Quantity
                    .ToDouble();

            // Remember the highest oxygen level we have seen.
            // A newly inserted fuller tank will automatically update this value.
            if (_hasTrustedOxygenReference &&
                oxygenMoles > _highestOxygenMoles)
            {
                    _highestOxygenMoles = oxygenMoles;

            // Remember this reference against this physical canister.
                _canisterOxygenReferences[canisterReferenceId] =
                    _highestOxygenMoles;
               
        // Save the learned reference so it survives a game restart.
        SaveCanisterReferences();

                Plugin.Log.LogInfo(
                    $"Oxygen Reserve: New full-tank reference = " +
                    $"{_highestOxygenMoles:F4} mol | " +
                    $"ReferenceId = {canisterReferenceId}");
            }
            if (_referenceLearningRequested)
            {
                _highestOxygenMoles = oxygenMoles;
                _hasTrustedOxygenReference = true;

                _canisterOxygenReferences[canisterReferenceId] =
                    _highestOxygenMoles;

                SaveCanisterReferences();

                _referenceLearningRequested = false;

                Plugin.Log.LogInfo(
                    $"Oxygen Reserve: TRUSTED full-tank reference learned. " +
                    $"ReferenceId = {canisterReferenceId} | " +
                    $"Oxygen = {_highestOxygenMoles:F4} mol | " +
                    $"Pressure = {pressureKPa:F2} kPa");
            }

            double oxygenPercent = 0.0;

            if (_hasTrustedOxygenReference &&
                    _highestOxygenMoles > 0.0)
            {
                oxygenPercent =
                    (oxygenMoles / _highestOxygenMoles) * 100.0;
            }
            if (!_hasTrustedOxygenReference)
            {
                Plugin.Log.LogWarning(
                    $"Oxygen Reserve: Canister ReferenceId = {canisterReferenceId} " +
                    $"does not have a trusted full-tank reference — automatic refill disabled.");

                return;
            }

            double oxygenTemperature =
                airTank.InternalAtmosphere
                    .GasMixture
                    .Oxygen
                    .Temperature
                    .ToDouble();

            // Automatic refill begins when the learned oxygen reserve
            // reaches 10% or less.
            const double ReserveTriggerPercent = 10.0;

            if (_hasTrustedOxygenReference &&
                oxygenPercent <= ReserveTriggerPercent)
            {
                double oxygenToAdd =
                    _highestOxygenMoles - oxygenMoles;

                if (oxygenToAdd > 0.0)
                {
                    // ----------------------------------------------------------
                    // Pressure Guard
                    // ----------------------------------------------------------

                    if (pressureKPa >= maxSafePressureKPa)
                    {
                        Plugin.Log.LogWarning(
                            $"Oxygen Reserve: PRESSURE GUARD — refill blocked. " +
                            $"Current pressure = {pressureKPa:F2} kPa | " +
                            $"Safe limit = {maxSafePressureKPa:F2} kPa | " +
                            $"Max pressure = {maxPressureKPa:F2} kPa");

                        return;
                    }
                
                    var gasMixture =
                        airTank.InternalAtmosphere.GasMixture;

                    var oxygen =
                        gasMixture.Oxygen;

                    var refillQuantity =
                        new Assets.Scripts.Atmospherics.MoleQuantity(
                            oxygenToAdd);

                    var refillTemperature =
                        oxygen.Temperature;

                    var refillMole =
                        new Assets.Scripts.Atmospherics.Mole(
                            Assets.Scripts.Atmospherics.Chemistry.GasType.Oxygen,
                            refillQuantity,
                            Assets.Scripts.Atmospherics.IdealGas.Energy(
                                refillTemperature,
                                oxygen.SpecificHeat(),
                                refillQuantity));

                    Plugin.Log.LogWarning(
                        $"Oxygen Reserve: RESERVE LEVEL REACHED — " +
                        $"{oxygenPercent:F1}% remaining.");

                    Plugin.Log.LogInfo(
                        $"Oxygen Reserve: Refilling " +
                        $"{oxygenToAdd:F4} mol O2 at " +
                        $"{refillTemperature.ToDouble():F2} K.");

                    var updatedGasMixture =
                        new Assets.Scripts.Atmospherics.GasMixture(
                            gasMixture);

                    updatedGasMixture.Add(refillMole);

                    // Use Stationeers' actual available gas volume for this canister.
                    var gasVolume =
                            airTank.InternalAtmosphere.GetGasVolume();

                    // Calculate what the pressure WOULD be after the refill.
                    // Nothing has been written to the real canister yet.
                    var predictedPressure =
                        Assets.Scripts.Atmospherics.IdealGas.Pressure(
                            updatedGasMixture.GetTotalMolesGasses,
                            updatedGasMixture.Temperature,
                            gasVolume);

                    double predictedPressureKPa =
                        predictedPressure.ToDouble();
                    Plugin.Log.LogInfo(
                            $"Oxygen Reserve: Pressure prediction — " +
                            $"Current = {pressureKPa:F2} kPa | " +
                            $"Predicted after refill = {predictedPressureKPa:F2} kPa | " +
                            $"Safe limit = {maxSafePressureKPa:F2} kPa");
                    // ----------------------------------------------------------
                    // Predictive Pressure Guard
                    // ----------------------------------------------------------

                    if (predictedPressureKPa > maxSafePressureKPa)
                    {
                        Plugin.Log.LogWarning(
                            $"Oxygen Reserve: PREDICTIVE PRESSURE GUARD — refill blocked. " +
                            $"Current pressure = {pressureKPa:F2} kPa | " +
                            $"Predicted pressure = {predictedPressureKPa:F2} kPa | " +
                            $"Safe limit = {maxSafePressureKPa:F2} kPa | " +
                            $"Max pressure = {maxPressureKPa:F2} kPa");

                        return;
                    }

                    // GasMixture is a struct, so write the updated mixture
                    // directly back to the tank's actual atmosphere.
                    airTank.InternalAtmosphere.GasMixture.Set(
                        updatedGasMixture,
                        Assets.Scripts.Atmospherics.AtmosphereHelper.MatterState.All);

                    Plugin.Log.LogInfo(
                        "Oxygen Reserve: Automatic refill completed.");
                }
            }

         }
        // --------------------------------------------------------------
        // Physical Reference Learning Key
        // --------------------------------------------------------------

        private static void CheckLearnReferenceKey()
        {
            KeyCode assignedKey =
                KeyCode.F7;

            if (StationeersKeybind.LearnReferenceKeyItem != null)
            {
                assignedKey =
                    StationeersKeybind.LearnReferenceKeyItem.Key;
            }

            int virtualKey =
                KeyCodeToVirtualKey(assignedKey);

            if (virtualKey == 0)
            {
                return;
            }

            bool keyDown =
                (GetAsyncKeyState(virtualKey) & 0x8000) != 0;

            if (keyDown && !_learnKeyWasDown)
            {
              Plugin.Log.LogInfo(
                $"Oxygen Reserve: Learn Full Tank Reference key pressed ({assignedKey}).");

                RequestReferenceLearning();
 
            }

            _learnKeyWasDown = keyDown;
        }

        // --------------------------------------------------------------
        // Request Reference Learning
        // --------------------------------------------------------------

        public static void RequestReferenceLearning()
        {
            // Do not allow an accidental key press to overwrite
            // an already trusted canister reference.
            if (_hasTrustedOxygenReference)
            {
                Plugin.Log.LogWarning(
                    $"Oxygen Reserve: Reference learning ignored — " +
                    $"canister ReferenceId = {_currentCanisterReferenceId} " +
                    $"already has a trusted reference.");

                return;
            }

            if (_currentCanisterReferenceId == 0)
            {
                Plugin.Log.LogWarning(
                    "Oxygen Reserve: Reference learning ignored — no air tank installed.");

                return;
            }

            _referenceLearningRequested = true;

            Plugin.Log.LogInfo(
                "Oxygen Reserve: Full-tank reference learning requested.");
        }

        // --------------------------------------------------------------
        // Load Canister References
        // --------------------------------------------------------------

        private static void LoadCanisterReferences()
        {
            try
            {
                if (!File.Exists(CanisterReferenceFile))
                {
                    Plugin.Log.LogInfo(
                        "Oxygen Reserve: No saved canister references found.");

                    return;
                }

                _canisterOxygenReferences.Clear();

                foreach (string line in File.ReadAllLines(CanisterReferenceFile))
                {
                    string[] parts = line.Split('=');

                    if (parts.Length != 2)
                    {
                        continue;
                    }

                    if (long.TryParse(parts[0], out long referenceId) &&
                        double.TryParse(
                            parts[1],
                            System.Globalization.NumberStyles.Float,
                            System.Globalization.CultureInfo.InvariantCulture,
                            out double oxygenReference))
                    {
                        _canisterOxygenReferences[referenceId] =
                            oxygenReference;
                    }
                }

                Plugin.Log.LogInfo(
                    $"Oxygen Reserve: Loaded " +
                    $"{_canisterOxygenReferences.Count} saved canister reference(s).");
            }
            catch (Exception ex)
            {
                Plugin.Log.LogError(
                    $"Oxygen Reserve: Could not load canister references: {ex}");
            }
        }

        // --------------------------------------------------------------
        // Save Canister References
        // --------------------------------------------------------------

        private static void SaveCanisterReferences()
        {
            try
            {
                using (var writer = new StreamWriter(
                    CanisterReferenceFile,
                    false))
                {
                    foreach (var entry in _canisterOxygenReferences)
                    {
                        writer.WriteLine(
                    $"{entry.Key}=" +
                    entry.Value.ToString(
                        "R",
                        System.Globalization.CultureInfo.InvariantCulture));
                    }
                }

                Plugin.Log.LogInfo(
                    $"Oxygen Reserve: Saved " +
                    $"{_canisterOxygenReferences.Count} canister reference(s).");
            }
            catch (Exception ex)
            {
                Plugin.Log.LogError(
                    $"Oxygen Reserve: Could not save canister references: {ex}");
            }
        }

        // --------------------------------------------------------------
        // Stop
        // --------------------------------------------------------------

        public static void Stop()
        {
            _running = false;
        }
        // --------------------------------------------------------------
        // Unity KeyCode -> Windows Virtual Key
        // --------------------------------------------------------------

        private static int KeyCodeToVirtualKey(KeyCode key)
        {
            // Function keys F1-F12.
            if (key >= KeyCode.F1 &&
                key <= KeyCode.F12)
            {
                return 0x70 +
                    (key - KeyCode.F1);
            }

            // Letters A-Z.
            if (key >= KeyCode.A &&
                key <= KeyCode.Z)
            {
                return 0x41 +
                    (key - KeyCode.A);
            }

            // Number row 0-9.
            if (key >= KeyCode.Alpha0 &&
                key <= KeyCode.Alpha9)
            {
                return 0x30 +
                    (key - KeyCode.Alpha0);
            }

            switch (key)
            {
                case KeyCode.Space:
                    return 0x20;

                case KeyCode.Tab:
                    return 0x09;

                case KeyCode.Return:
                    return 0x0D;

                case KeyCode.Escape:
                    return 0x1B;

                case KeyCode.Backspace:
                    return 0x08;

                case KeyCode.Insert:
                    return 0x2D;

                case KeyCode.Delete:
                    return 0x2E;

                case KeyCode.Home:
                    return 0x24;

                case KeyCode.End:
                    return 0x23;

                case KeyCode.PageUp:
                    return 0x21;

                case KeyCode.PageDown:
                    return 0x22;

                case KeyCode.UpArrow:
                    return 0x26;

                case KeyCode.DownArrow:
                    return 0x28;

                case KeyCode.LeftArrow:
                    return 0x25;

                case KeyCode.RightArrow:
                    return 0x27;

                default:
                    return 0;
            }
        }
    }

}