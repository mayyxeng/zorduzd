using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Text;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using UnityEngine;

namespace ZorDuzd;

public static class MyPluginInfo
{
    public const string PLUGIN_GUID = "zorduzd";
    public const string PLUGIN_NAME = "zorduzd";
    public const string PLUGIN_VERSION = "0.3.1";
}

[BepInPlugin(MyPluginInfo.PLUGIN_GUID, MyPluginInfo.PLUGIN_NAME, MyPluginInfo.PLUGIN_VERSION)]
[BepInProcess("NuclearOption.exe")]
public class Plugin : BaseUnityPlugin
{
    internal static new ManualLogSource Logger;
    internal long tickCount = 0;
    internal TcpListener socket;
    internal TcpClient theClient;

    // Telemtry data
    internal TelemetryData telemetryData;

    // Cached reflection fields
    private static FieldInfo canopiesField;
    private static FieldInfo canopyOpenAmountField;
    private static FieldInfo canopyFiringField;
    private static FieldInfo nozzlesField;
    private static FieldInfo afterburnersField;
    private static FieldInfo abAmountField;
    private static FieldInfo cmStationsField;
    private static FieldInfo cmStationAmmoField;
    private static FieldInfo cmStationDisplayNameField;
    private static FieldInfo controlSurfacesField;
    private static FieldInfo throttleGaugeAirbrakeField;
    private bool cachedHasAirbrake;
    private ThrottleGauge cachedThrottleGauge;

    // Configurations
    // private ConfigEntry<string> configIp;
    private ConfigEntry<int> configPort;
    private ConfigEntry<bool> configEnableDebugUi;

    // Debug ui
    private Rect windowRect = new Rect(100, 100, 450, 1200);

    private void Awake()
    {
        // Plugin startup logic
        Logger = base.Logger;
        Logger.LogInfo($"Plugin {MyPluginInfo.PLUGIN_GUID} is loaded!");

        Logger.LogInfo("Loading Configurations");
        // configIp = Config.Bind("Networking", "ip", IPAddress.Loopback.ToString(), "IP address to listen on");
        configPort = Config.Bind("Networking", "port", 3480, "TCP port to listen on");
        configEnableDebugUi = Config.Bind("Debug", "enableUi", false, "Enable debug UI");
        Logger.LogInfo("Configurations loaded");

        StartTcpListener();
        configPort.SettingChanged += (sender, args) => StartTcpListener();

        // Cache reflection fields once
        const BindingFlags priv = BindingFlags.NonPublic | BindingFlags.Instance;
        canopiesField = typeof(Aircraft).GetField("canopies", priv);
        canopyOpenAmountField = typeof(Canopy).GetField("openAmount", priv);
        canopyFiringField = typeof(Canopy).GetField("firing", priv);
        nozzlesField = typeof(Turbojet).GetField("nozzles", priv);
        afterburnersField = typeof(JetNozzle).GetField("afterburners", priv);
        Type afterburnerType = typeof(JetNozzle).GetNestedType(
            "Afterburner",
            BindingFlags.NonPublic
        );
        abAmountField = afterburnerType.GetField("afterburnerAmount");
        controlSurfacesField = typeof(Aircraft).GetField("controlSurfaces", priv);
        throttleGaugeAirbrakeField = typeof(ThrottleGauge).GetField("airbrake", priv);
        cmStationsField = typeof(CountermeasureManager).GetField("countermeasureStations", priv);
        Type cmStationType = typeof(CountermeasureManager).GetNestedType(
            "CountermeasureStation",
            BindingFlags.NonPublic
        );
        cmStationAmmoField = cmStationType.GetField("ammo");
        cmStationDisplayNameField = cmStationType.GetField("displayName");
    }

    private void StartTcpListener()
    {
        if (theClient != null)
        {
            Logger.LogInfo("Closing existing TcpClient");
            theClient.Close();
        }
        if (socket != null)
        {
            Logger.LogInfo("Stopping TcpListener");
            socket.Stop();
        }
        theClient = null;
        int port = configPort.Value;
        IPAddress ip = IPAddress.Loopback;
        socket = new TcpListener(ip, port);
        socket.Start();
        Logger.LogInfo($"TcpListener started on port {ip}:{port}");
    }

    internal struct TelemetryData
    {
        public string aircraftName;
        public string name;
        public string unitName;
        public Vector3 accel;
        public float gForce;
        public float altitude;
        public float speedOfSound;
        public Vector3 windVelocity;
        public List<EngineTelemetry> engines;
        public List<CanopyTelemetry> canopies;
        public LandingGear.GearState gearState;

        public Vector3 position;
        public Quaternion rotation;
        public Vector3 velocity;
        public Vector3 angularVelocity;
        public float aoa;
        public float aos;

        public float tas;
        public float ias;

        public float mach;

        public float pitch;
        public float roll;
        public float yaw;
        public float brake;
        public float speedbrake;

        public string weapon_name;
        public int shells;
        public List<CountermeasureTelemetry> countermeasures;

        public String IntoMessage()
        {
            string message = "";
            void append<T>(string name, T value) => message += $"{name},{value};";
            append("aircraft_name", aircraftName.Replace(",", "_"));
            append("name", name.Replace(",", "_"));
            append("unit_name", unitName.Replace(",", "_"));
            append("acc_x", accel.x);
            append("acc_y", accel.y);
            append("acc_z", accel.z);
            append("g_force", gForce);
            append("h_above_sea_level", altitude);
            append("wind_x", windVelocity.x);
            append("wind_y", windVelocity.y);
            append("wind_z", windVelocity.z);
            append("speed_of_sound", speedOfSound);

            append("tas", tas);
            append("ias", ias);
            append("mach", mach);
            append("aoa", aoa);
            append("aos", aos);

            if (engines != null)
            {
                for (int i = 0; i < engines.Count; i++)
                {
                    append($"rpm_{i}", engines[i].rpm);
                    append($"rpm_ratio_{i}", engines[i].rpmRatio);
                    append($"thrust_{i}", engines[i].thrust);
                    append($"thurst_max_{i}", engines[i].maxThrust);
                    for (int j = 0; j < engines[i].afterburnerLevels.Count; j++)
                    {
                        append($"afterburner_{i}_{j}", engines[i].afterburnerLevels[j]);
                    }
                }

                // Summarized engine RPM and afterburner
                if (engines.Count == 2)
                {
                    append("engine_rpm_left", engines[0].rpmRatio * 100);
                    append("engine_rpm_right", engines[1].rpmRatio * 100);
                    float abLeft =
                        engines[0].afterburnerLevels.Count > 0
                            ? engines[0].afterburnerLevels[0]
                            : 0f;
                    float abRight =
                        engines[1].afterburnerLevels.Count > 0
                            ? engines[1].afterburnerLevels[0]
                            : 0f;
                    append("afterburner_1", abLeft);
                    append("afterburner_2", abRight);
                }
                else
                {
                    float rpmSum = 0f;
                    float abSum = 0f;
                    int abCount = 0;
                    for (int i = 0; i < engines.Count; i++)
                    {
                        rpmSum += engines[i].rpmRatio;
                        for (int j = 0; j < engines[i].afterburnerLevels.Count; j++)
                        {
                            abSum += engines[i].afterburnerLevels[j];
                            abCount++;
                        }
                    }
                    float avgRpm = engines.Count > 0 ? rpmSum / engines.Count * 100 : 0f;
                    float avgAb = abCount > 0 ? abSum / abCount : 0f;
                    append("engine_rpm_left", avgRpm);
                    append("engine_rpm_right", avgRpm);
                    append("afterburner_1", avgAb);
                    append("afterburner_2", avgAb);
                }
            }

            if (canopies != null)
            {
                float canopySum = 0f;
                for (int i = 0; i < canopies.Count; i++)
                {
                    append($"canopy_open_{i}", canopies[i].openAmount);
                    append($"canopy_ejected_{i}", canopies[i].ejected);
                    canopySum += canopies[i].openAmount;
                }
                append("canopy", canopies.Count > 0 ? canopySum / canopies.Count : 0f);
            }

            if (countermeasures != null)
            {
                for (int i = 0; i < countermeasures.Count; i++)
                {
                    append($"cm_name_{i}", countermeasures[i].name.Replace(",", "_"));
                    append($"cm_ammo_{i}", countermeasures[i].ammo);
                    if (countermeasures[i].name == "IR Flares")
                    {
                        append("flare", countermeasures[i].ammo);
                    }
                    else if (countermeasures[i].name == "Radar Jammer")
                    {
                        append("chaff", countermeasures[i].ammo);
                    }
                }
            }

            append("position_x", position.x);
            append("position_y", position.y);
            append("position_z", position.z);
            append("rotation_x", rotation.x);
            append("rotation_y", rotation.y);
            append("rotation_z", rotation.z);
            append("rotation_w", rotation.w);
            append("vector_velocity_x", velocity.x);
            append("vector_velocity_y", velocity.y);
            append("vector_velocity_z", velocity.z);
            append("euler_vx", angularVelocity.x);
            append("euler_vy", angularVelocity.y);
            append("euler_vz", angularVelocity.z);
            append("vertical_velocity_speed", velocity.y);

            append("pitch", pitch);
            append("bank", roll);
            append("yaw", yaw);
            append("brake", brake);
            append("speedbrake_value", speedbrake);

            append("gear_state", gearState);
            float gear_value = 0.0F;
            switch (gearState)
            {
                case LandingGear.GearState.Uninitialized:
                    gear_value = 0.0F;
                    break;
                case LandingGear.GearState.LockedExtended:
                    gear_value = 1.0F;
                    break;
                case LandingGear.GearState.LockedRetracted:
                    gear_value = 0.0F;
                    break;
                case LandingGear.GearState.Retracting:
                    gear_value = 0.5F;
                    break;
                case LandingGear.GearState.Extending:
                    gear_value = 0.5F;
                    break;
            }
            append("gear_value", gear_value);
            float on_ground = altitude == 0.0F ? 1.0F : 0.0F;
            append("nose_gear", gear_value * on_ground);
            append("left_gear", gear_value * on_ground);
            append("right_gear", gear_value * on_ground);
            append("weapon", weapon_name);
            append("cannon_shells", shells);

            return message;
        }
    }

    internal struct EngineTelemetry
    {
        public float rpm;
        public float rpmRatio;
        public float thrust;
        public float maxThrust;
        public List<float> afterburnerLevels;
    }

    internal struct CanopyTelemetry
    {
        public float openAmount;
        public bool ejected;
    }

    internal struct CountermeasureTelemetry
    {
        public string name;
        public int ammo;
    }

    private void FixedUpdate()
    {
        tickCount++;
        if (!MissionManager.IsRunning)
        {
            return;
        }
        Aircraft aircraft;
        GameManager.GetLocalAircraft(out aircraft);
        if (aircraft == null)
        {
            // Logger.LogError("Could not retrive the local aircraft");
            return;
        }
        if (aircraft.pilots.Length == 0)
        {
            Logger.LogError("Could not retrive the local pilot");
            return;
        }
        if (aircraft.pilots[0].currentState is PilotPlayerState == false)
        {
            Logger.LogError("Could not retrive the pilot player state");
            return;
        }

        Pilot pilot = aircraft.pilots[0];
        PilotPlayerState playerState = (PilotPlayerState)pilot.currentState;
        // Logger.LogWarning($"Pilot: {pilot}");
        if (pilot.dead || pilot.ejected)
        {
            for (int index = 0; index < aircraft.pilots.Length; index += 1)
            {
                Pilot p = aircraft.pilots[index];
                Logger.LogError(
                    $"Could not find the right pilot! playerController: @{index} {p.playerControlled}, {p.dead}, {p.ejected}"
                );
            }
            return;
        }

        telemetryData.aircraftName = aircraft.GetAircraftParameters().aircraftName;
        telemetryData.name = aircraft.name;
        telemetryData.unitName = aircraft.unitName;
        telemetryData.accel = aircraft.accel;
        telemetryData.gForce = aircraft.gForce;
        telemetryData.altitude = aircraft.radarAlt;
        telemetryData.windVelocity = aircraft.GetWindVelocity();
        telemetryData.speedOfSound = LevelInfo.GetSpeedOfSound(
            aircraft.transform.GlobalPosition().y
        );

        if (telemetryData.engines == null)
        {
            telemetryData.engines = new List<EngineTelemetry>();
        }
        else
        {
            telemetryData.engines.Clear();
        }

        aircraft.engineStates.ForEach(engine =>
        {
            var et = new EngineTelemetry
            {
                rpm = engine.GetRPM(),
                rpmRatio = engine.GetRPMRatio(),
                thrust = engine.GetThrust(),
                maxThrust = engine.GetMaxThrust(),
                afterburnerLevels = new List<float>(),
            };

            if (engine is Turbojet turbojet)
            {
                JetNozzle[] nozzles = (JetNozzle[])nozzlesField.GetValue(turbojet);
                if (nozzles != null)
                {
                    foreach (JetNozzle nozzle in nozzles)
                    {
                        Array abs = (Array)afterburnersField.GetValue(nozzle);
                        if (abs != null)
                        {
                            foreach (object ab in abs)
                            {
                                et.afterburnerLevels.Add((float)abAmountField.GetValue(ab));
                            }
                        }
                    }
                }
            }

            telemetryData.engines.Add(et);
        });
        if (telemetryData.countermeasures == null)
        {
            telemetryData.countermeasures = new List<CountermeasureTelemetry>();
        }
        else
        {
            telemetryData.countermeasures.Clear();
        }
        // Canopy state
        if (telemetryData.canopies == null)
        {
            telemetryData.canopies = new List<CanopyTelemetry>();
        }
        else
        {
            telemetryData.canopies.Clear();
        }

        Canopy[] canopies = (Canopy[])canopiesField.GetValue(aircraft);
        if (canopies != null)
        {
            foreach (Canopy canopy in canopies)
            {
                if (canopy != null)
                {
                    telemetryData.canopies.Add(
                        new CanopyTelemetry
                        {
                            openAmount = (float)canopyOpenAmountField.GetValue(canopy),
                            ejected = (bool)canopyFiringField.GetValue(canopy),
                        }
                    );
                }
            }
        }

        telemetryData.gearState = aircraft.gearState;

        aircraft.transform.GetPositionAndRotation(
            out telemetryData.position,
            out telemetryData.rotation
        );

        telemetryData.velocity = aircraft.rb.velocity;
        telemetryData.angularVelocity = aircraft.rb.angularVelocity * Mathf.Rad2Deg;

        Vector3 localAirVelocity = aircraft.cockpit.transform.InverseTransformDirection(
            aircraft.rb.velocity - aircraft.GetWindVelocity()
        );
        telemetryData.tas = localAirVelocity.magnitude;
        telemetryData.ias = aircraft.speed;
        telemetryData.mach = telemetryData.tas / telemetryData.speedOfSound;

        telemetryData.aoa = Mathf.Atan2(-localAirVelocity.y, localAirVelocity.z) * Mathf.Rad2Deg;
        telemetryData.aos = Mathf.Atan2(localAirVelocity.x, localAirVelocity.z) * Mathf.Rad2Deg;

        telemetryData.pitch = aircraft.GetInputs().pitch;
        telemetryData.roll = aircraft.GetInputs().roll;
        telemetryData.yaw = aircraft.GetInputs().yaw;
        telemetryData.brake = aircraft.GetInputs().brake;

        // Speedbrake: mirror the HUD logic from ThrottleGauge
        // "AIRBRAKE" shows when the gauge has airbrake=true and throttle==0
        if (cachedThrottleGauge == null)
        {
            cachedThrottleGauge = FindObjectOfType<ThrottleGauge>();
            // TODO: should we check that cachedThrottleGauge.aircraft is our aircraft?
            cachedHasAirbrake =
                cachedThrottleGauge != null
                && (bool)throttleGaugeAirbrakeField.GetValue(cachedThrottleGauge);
        }
        telemetryData.speedbrake =
            cachedHasAirbrake && aircraft.GetInputs().throttle == 0f ? 1f : 0f;
        Logger.LogInfo($"Found {aircraft.GetInputs().throttle} and {cachedHasAirbrake}");

        // Countermeasures
        if (telemetryData.countermeasures == null)
        {
            telemetryData.countermeasures = new List<CountermeasureTelemetry>();
        }
        else
        {
            telemetryData.countermeasures.Clear();
        }

        System.Collections.IList cmStations = (System.Collections.IList)
            cmStationsField.GetValue(aircraft.countermeasureManager);
        if (cmStations != null)
        {
            foreach (object station in cmStations)
            {
                telemetryData.countermeasures.Add(
                    new CountermeasureTelemetry
                    {
                        name = (string)cmStationDisplayNameField.GetValue(station),
                        ammo = (int)cmStationAmmoField.GetValue(station),
                    }
                );
            }
        }

        WeaponStation currentGuns = aircraft.weaponManager.currentWeaponStation;
        if (currentGuns != null)
        {
            telemetryData.weapon_name = currentGuns.WeaponInfo.name;
            telemetryData.shells = currentGuns.Ammo;
        }
        // we have an alive pilot in the cockpit!
        SendTelemetryOverTcp();
    }

    private void SendTelemetryOverTcp()
    {
        if (socket == null)
        {
            Logger.LogError("TcpListener is dead");
            return;
        }
        if (socket.Pending() && theClient == null)
        {
            // accept a single client
            theClient = socket.AcceptTcpClient();
            Logger.LogInfo("Client connected to TcpListener");
        }
        if (theClient == null || !theClient.Connected)
        {
            return;
        }
        try
        {
            NetworkStream stream = theClient.GetStream();
            byte[] data = Encoding.UTF8.GetBytes(telemetryData.IntoMessage() + "\n");
            stream.Write(data, 0, data.Length);
        }
        catch (System.Exception ex)
        {
            theClient.Close();
            theClient = null;
            Logger.LogInfo($"Client disconnected from TcpListener: {ex.Message}");
        }
    }

    private void OnGUI()
    {
        if (!configEnableDebugUi.Value)
        {
            return;
        }
        windowRect = GUI.Window(0, windowRect, DrawWindow, "Debug Window");
    }

    void DrawWindow(int windowID)
    {
        GUILayout.BeginVertical();

        GUILayout.Label($"Aircraft Name: {telemetryData.aircraftName}");
        GUILayout.Label($"Name: {telemetryData.name}");
        GUILayout.Label($"Unit Name: {telemetryData.unitName}");

        GUILayout.Space(10);
        GUILayout.Label("--- Air Data ---");
        GUILayout.Label($"TAS: {telemetryData.tas:F2} m/s ({telemetryData.tas * 3.6f:F1} km/h)");
        GUILayout.Label($"IAS: {telemetryData.ias:F2} m/s ({telemetryData.ias * 3.6f:F1} km/h)");
        GUILayout.Label($"Mach: {telemetryData.mach:F3}");
        GUILayout.Label($"AoA: {telemetryData.aoa:F2}°");
        GUILayout.Label($"AoS: {telemetryData.aos:F2}°");

        GUILayout.Space(10);
        GUILayout.Label("--- Physics ---");
        GUILayout.Label($"Position: {telemetryData.position}");
        GUILayout.Label($"Rotation: {telemetryData.rotation}");
        GUILayout.Label(
            $"Velocity: {telemetryData.velocity} (Mag: {telemetryData.velocity.magnitude:F2} m/s)"
        );
        GUILayout.Label($"Angular Velocity: {telemetryData.angularVelocity}");
        GUILayout.Label($"Acceleration: {telemetryData.accel}");
        GUILayout.Label($"G-Force: {telemetryData.gForce:F2}");
        GUILayout.Label($"Altitude (Radar): {telemetryData.altitude:F2} m");

        GUILayout.Space(10);
        GUILayout.Label("--- Controls ---");
        GUILayout.Label($"Pitch: {telemetryData.pitch:F2}");
        GUILayout.Label($"Roll: {telemetryData.roll:F2}");
        GUILayout.Label($"Yaw: {telemetryData.yaw:F2}");
        GUILayout.Label($"Brake: {telemetryData.brake:F2}");
        GUILayout.Label($"Speedbrake: {telemetryData.speedbrake * 100:F1}%");

        GUILayout.Space(10);
        GUILayout.Label("--- Environment ---");
        GUILayout.Label($"Wind Velocity: {telemetryData.windVelocity}");
        GUILayout.Label($"Speed of Sound: {telemetryData.speedOfSound:F2} m/s");

        GUILayout.Space(10);
        GUILayout.Label($"Gear State: {telemetryData.gearState}");

        if (telemetryData.engines != null && telemetryData.engines.Count > 0)
        {
            GUILayout.Space(10);
            GUILayout.Label("--- Engines ---");
            for (int i = 0; i < telemetryData.engines.Count; i++)
            {
                var engine = telemetryData.engines[i];
                GUILayout.Label($"Engine {i}:");
                GUILayout.Label($"  RPM: {engine.rpm:F0} ({engine.rpmRatio * 100:F1}%)");
                GUILayout.Label($"  Thrust: {engine.thrust:F0} N / {engine.maxThrust:F0} N");
                if (engine.afterburnerLevels != null && engine.afterburnerLevels.Count > 0)
                {
                    for (int j = 0; j < engine.afterburnerLevels.Count; j++)
                    {
                        GUILayout.Label(
                            $"  Afterburner {j}: {engine.afterburnerLevels[j] * 100:F1}%"
                        );
                    }
                }
            }
        }

        if (telemetryData.canopies != null && telemetryData.canopies.Count > 0)
        {
            GUILayout.Space(10);
            GUILayout.Label("--- Canopies ---");
            for (int i = 0; i < telemetryData.canopies.Count; i++)
            {
                var canopy = telemetryData.canopies[i];
                string state = canopy.ejected ? "EJECTED" : $"Open: {canopy.openAmount * 100:F1}%";
                GUILayout.Label($"Canopy {i}: {state}");
            }
        }

        if (telemetryData.countermeasures != null && telemetryData.countermeasures.Count > 0)
        {
            GUILayout.Space(10);
            GUILayout.Label("--- Countermeasures ---");
            for (int i = 0; i < telemetryData.countermeasures.Count; i++)
            {
                var cm = telemetryData.countermeasures[i];
                GUILayout.Label($"{cm.name}: {cm.ammo}");
            }
        }

        GUILayout.Space(10);
        GUILayout.BeginHorizontal();
        GUILayout.Label($"weapon: {telemetryData.weapon_name}");
        GUILayout.Label($"shells: {telemetryData.shells}");
        GUILayout.EndHorizontal();
        GUILayout.EndVertical();
        GUI.DragWindow();
    }

    private void OnDestroy()
    {
        if (theClient != null)
        {
            theClient.Close();
        }
        if (socket != null)
        {
            socket.Stop();
        }
        Logger.LogInfo($"Plugin recorded {tickCount} ticks");
        Logger.LogInfo($"Plugin {MyPluginInfo.PLUGIN_GUID} is unloaded!");
    }
}
