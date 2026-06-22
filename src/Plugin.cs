using System;
using System.Collections.Generic;
using System.IO;
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
    public const string PLUGIN_VERSION = "0.3.5";
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
    private ConfigEntry<bool> configSnifferEnable;
    private ConfigEntry<string> configSnifferTargetClassNameContains;
    private ConfigEntry<string> configSnifferSnapshotKey;
    private ConfigEntry<bool> configSnifferIncludeProperties;
    private ConfigEntry<bool> configSnifferDepthOneExpansion;
    private ConfigEntry<bool> configSnifferLogComplexMarkers;

    // Debug ui
    private Rect windowRect = new Rect(100, 100, 450, 1200);

    // Sniffer state
    private KeyCode snifferSnapshotKeyCode = KeyCode.F9;
    private StreamWriter snifferWriter;
    private bool componentDumpDone;
    private readonly Dictionary<string, object> snifferLastFieldValues = new Dictionary<string, object>();
    private readonly Dictionary<string, object> snifferLastPropertyValues = new Dictionary<string, object>();
    private readonly HashSet<string> snifferWarnedPropertyKeys = new HashSet<string>();
    private static readonly Dictionary<Type, SnifferTypeInfo> snifferTypeInfoCache =
        new Dictionary<Type, SnifferTypeInfo>();

    private struct SnifferTypeInfo
    {
        public FieldInfo[] SimpleFields;
        public FieldInfo[] ComplexFields;
        public PropertyInfo[] SimpleProperties;
        public PropertyInfo[] ComplexProperties;
    }

    private void Awake()
    {
        // Plugin startup logic
        Logger = base.Logger;
        Logger.LogInfo($"Plugin {MyPluginInfo.PLUGIN_GUID} is loaded!");

        Logger.LogInfo("Loading Configurations");
        // configIp = Config.Bind("Networking", "ip", IPAddress.Loopback.ToString(), "IP address to listen on");
        configPort = Config.Bind("Networking", "port", 3480, "TCP port to listen on");
        configEnableDebugUi = Config.Bind("Debug", "enableUi", false, "Enable debug UI");
        configSnifferEnable = Config.Bind(
            "Sniffer",
            "enable",
            false,
            "Master switch for the telemetry field sniffer. Disabled by default; no files are written when off."
        );
        configSnifferTargetClassNameContains = Config.Bind(
            "Sniffer",
            "targetClassNameContains",
            "",
            "Case-insensitive substring filter for component class names to sniff. Empty disables field-diff logging (component tree dump still runs)."
        );
        configSnifferSnapshotKey = Config.Bind(
            "Sniffer",
            "snapshotKey",
            "F9",
            "Legacy Input Manager KeyCode name that triggers a forced snapshot dump of all matching components."
        );
        configSnifferIncludeProperties = Config.Bind(
            "Sniffer",
            "includeProperties",
            true,
            "Also sniff simple properties (not just fields) of matching components."
        );
        configSnifferDepthOneExpansion = Config.Bind(
            "Sniffer",
            "depthOneExpansion",
            true,
            "Reflect one level into complex (non-simple) members whose runtime type lives in Assembly-CSharp. If false, complex members are only logged via logComplexMarkers (if enabled)."
        );
        configSnifferLogComplexMarkers = Config.Bind(
            "Sniffer",
            "logComplexMarkers",
            false,
            "Log a marker line (type + assembly) for complex members that are not depth-1 expanded. Off by default to limit log size."
        );
        Logger.LogInfo("Configurations loaded");

        if (configSnifferEnable.Value)
        {
            if (!Enum.TryParse<KeyCode>(configSnifferSnapshotKey.Value, true, out snifferSnapshotKeyCode))
            {
                Logger.LogWarning(
                    $"Invalid Sniffer.snapshotKey '{configSnifferSnapshotKey.Value}', falling back to F9"
                );
                snifferSnapshotKeyCode = KeyCode.F9;
            }
            string snifferLogPath = Path.Combine(Paths.PluginPath, "zorduzd_sniffer.log");
            snifferWriter = new StreamWriter(snifferLogPath, append: false) { AutoFlush = true };
        }

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
            componentDumpDone = false;
            return;
        }
        Aircraft aircraft;
        GameManager.GetLocalAircraft(out aircraft);
        if (aircraft == null)
        {
            // Logger.LogError("Could not retrive the local aircraft");
            return;
        }

        RunSniffer(aircraft);

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

    private void RunSniffer(Aircraft aircraft)
    {
        if (!configSnifferEnable.Value)
        {
            return;
        }

        SnifferDumpComponentsOnce(aircraft);

        string filter = configSnifferTargetClassNameContains.Value;
        if (string.IsNullOrEmpty(filter))
        {
            return;
        }

        var matches = new List<MonoBehaviour>();
        foreach (MonoBehaviour comp in aircraft.GetComponentsInChildren<MonoBehaviour>(true))
        {
            if (comp.GetType().Name.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0)
            {
                matches.Add(comp);
            }
        }

        foreach (MonoBehaviour comp in matches)
        {
            SnifferLogComponent(comp, snapshot: false);
        }

        // Legacy Input Manager, not the new Input System: a parallel control measurement in
        // NOVR found the new Input System dropping keyboard events under OpenXR via Virtual
        // Desktop, while the legacy manager kept working. Not verified in NO's non-VR mode,
        // but legacy is the safe choice here at no extra cost.
        if (UnityEngine.Input.GetKeyDown(snifferSnapshotKeyCode))
        {
            foreach (MonoBehaviour comp in matches)
            {
                SnifferLogComponent(comp, snapshot: true);
            }
        }
    }

    private void SnifferDumpComponentsOnce(Aircraft aircraft)
    {
        if (componentDumpDone)
        {
            return;
        }
        componentDumpDone = true;
        try
        {
            string path = Path.Combine(Paths.PluginPath, "zorduzd_components.log");
            using (var writer = new StreamWriter(path, append: false))
            {
                foreach (MonoBehaviour comp in aircraft.GetComponentsInChildren<MonoBehaviour>(true))
                {
                    writer.WriteLine($"{SnifferHierarchyPath(comp.transform)} :: {comp.GetType().FullName}");
                }
            }
        }
        catch (Exception ex)
        {
            Logger.LogError($"Sniffer component dump failed: {ex.Message}");
        }
    }

    private static string SnifferHierarchyPath(Transform t)
    {
        string path = t.name;
        Transform parent = t.parent;
        while (parent != null)
        {
            path = $"{parent.name}/{path}";
            parent = parent.parent;
        }
        return path;
    }

    private static bool SnifferIsSimpleType(Type t)
    {
        return t.IsPrimitive
            || t.IsEnum
            || t == typeof(string)
            || t == typeof(Vector3)
            || t == typeof(Quaternion)
            || t == typeof(Vector2)
            || t == typeof(bool);
    }

    private static readonly string[] SnifferExcludedNamespacePrefixes =
    {
        "UnityEngine",
        "Unity.",
        "Mirage",
        "System",
    };

    private static bool SnifferIsExcludedNamespace(string ns)
    {
        if (string.IsNullOrEmpty(ns))
        {
            return false;
        }
        foreach (string prefix in SnifferExcludedNamespacePrefixes)
        {
            if (ns == prefix || ns.StartsWith(prefix + ".", StringComparison.Ordinal))
            {
                return true;
            }
        }
        return false;
    }

    private static bool SnifferIsCollection(Type t)
    {
        return t.IsArray || typeof(System.Collections.IEnumerable).IsAssignableFrom(t);
    }

    private static SnifferTypeInfo SnifferGetTypeInfo(Type t)
    {
        if (snifferTypeInfoCache.TryGetValue(t, out SnifferTypeInfo cached))
        {
            return cached;
        }

        var simpleFields = new List<FieldInfo>();
        var complexFields = new List<FieldInfo>();
        foreach (
            FieldInfo field in t.GetFields(
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance
            )
        )
        {
            if (SnifferIsSimpleType(field.FieldType))
            {
                simpleFields.Add(field);
            }
            else
            {
                complexFields.Add(field);
            }
        }

        var simpleProperties = new List<PropertyInfo>();
        var complexProperties = new List<PropertyInfo>();
        foreach (
            PropertyInfo prop in t.GetProperties(
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance
            )
        )
        {
            if (!prop.CanRead || prop.GetIndexParameters().Length != 0)
            {
                continue;
            }
            if (SnifferIsSimpleType(prop.PropertyType))
            {
                simpleProperties.Add(prop);
            }
            else
            {
                complexProperties.Add(prop);
            }
        }

        var info = new SnifferTypeInfo
        {
            SimpleFields = simpleFields.ToArray(),
            ComplexFields = complexFields.ToArray(),
            SimpleProperties = simpleProperties.ToArray(),
            ComplexProperties = complexProperties.ToArray(),
        };
        snifferTypeInfoCache[t] = info;
        return info;
    }

    private bool SnifferTryGetPropertyValue(
        object instance,
        PropertyInfo prop,
        string warnKey,
        out object value
    )
    {
        try
        {
            value = prop.GetValue(instance);
            return true;
        }
        catch (Exception ex)
        {
            value = null;
            if (snifferWarnedPropertyKeys.Add(warnKey))
            {
                Logger.LogWarning($"Sniffer: property getter '{warnKey}' threw: {ex.Message}");
            }
            return false;
        }
    }

    private void SnifferLogComponent(MonoBehaviour comp, bool snapshot)
    {
        Type type = comp.GetType();
        string className = type.Name;
        int instanceId = comp.GetInstanceID();
        SnifferTypeInfo info = SnifferGetTypeInfo(type);

        foreach (FieldInfo field in info.SimpleFields)
        {
            object newValue = field.GetValue(comp);
            SnifferLogSimpleValue(
                snifferLastFieldValues,
                "F",
                className,
                field.Name,
                instanceId,
                newValue,
                snapshot
            );
        }

        if (configSnifferIncludeProperties.Value)
        {
            foreach (PropertyInfo prop in info.SimpleProperties)
            {
                string warnKey = $"{className}.{prop.Name}#{instanceId}";
                if (!SnifferTryGetPropertyValue(comp, prop, warnKey, out object newValue))
                {
                    continue;
                }
                SnifferLogSimpleValue(
                    snifferLastPropertyValues,
                    "P",
                    className,
                    prop.Name,
                    instanceId,
                    newValue,
                    snapshot
                );
            }
        }

        foreach (FieldInfo field in info.ComplexFields)
        {
            object value = field.GetValue(comp);
            SnifferHandleComplexMember(className, instanceId, field.Name, value, snapshot);
        }

        if (configSnifferIncludeProperties.Value)
        {
            foreach (PropertyInfo prop in info.ComplexProperties)
            {
                string warnKey = $"{className}.{prop.Name}#{instanceId}";
                if (!SnifferTryGetPropertyValue(comp, prop, warnKey, out object value))
                {
                    continue;
                }
                SnifferHandleComplexMember(className, instanceId, prop.Name, value, snapshot);
            }
        }
    }

    private void SnifferHandleComplexMember(
        string className,
        int instanceId,
        string memberName,
        object value,
        bool snapshot
    )
    {
        if (value == null)
        {
            return;
        }
        Type valueType = value.GetType();

        if (configSnifferDepthOneExpansion.Value)
        {
            if (SnifferIsCollection(valueType))
            {
                SnifferLogCollectionMarker(className, instanceId, memberName, valueType, value, snapshot);
                return;
            }

            bool eligibleForExpansion =
                !valueType.IsValueType
                && valueType.Assembly == typeof(Aircraft).Assembly
                && !SnifferIsExcludedNamespace(valueType.Namespace);

            if (eligibleForExpansion)
            {
                SnifferLogDepth1(valueType, className, instanceId, memberName, value, snapshot);
                return;
            }
        }

        if (configSnifferLogComplexMarkers.Value)
        {
            SnifferLogComplexMarker(className, instanceId, memberName, valueType, snapshot);
        }
    }

    private void SnifferLogDepth1(
        Type innerType,
        string className,
        int instanceId,
        string outerMemberName,
        object innerValue,
        bool snapshot
    )
    {
        SnifferTypeInfo info = SnifferGetTypeInfo(innerType);

        foreach (FieldInfo field in info.SimpleFields)
        {
            object newValue = field.GetValue(innerValue);
            string path = $"{outerMemberName}.{field.Name}";
            SnifferLogSimpleValue(snifferLastFieldValues, "F", className, path, instanceId, newValue, snapshot);
        }

        if (configSnifferIncludeProperties.Value)
        {
            foreach (PropertyInfo prop in info.SimpleProperties)
            {
                string path = $"{outerMemberName}.{prop.Name}";
                string warnKey = $"{className}.{path}#{instanceId}";
                if (!SnifferTryGetPropertyValue(innerValue, prop, warnKey, out object newValue))
                {
                    continue;
                }
                SnifferLogSimpleValue(
                    snifferLastPropertyValues,
                    "P",
                    className,
                    path,
                    instanceId,
                    newValue,
                    snapshot
                );
            }
        }
    }

    private void SnifferLogCollectionMarker(
        string className,
        int instanceId,
        string memberName,
        Type collectionType,
        object value,
        bool snapshot
    )
    {
        string elementType = SnifferGetElementTypeName(collectionType);
        int count = SnifferGetCollectionCount(value);
        SnifferWriteRaw(snapshot, className, "L", memberName, instanceId, elementType, count.ToString());
    }

    private static string SnifferGetElementTypeName(Type collectionType)
    {
        if (collectionType.IsArray)
        {
            return collectionType.GetElementType()?.Name ?? "?";
        }
        if (collectionType.IsGenericType)
        {
            Type[] args = collectionType.GetGenericArguments();
            if (args.Length > 0)
            {
                return args[0].Name;
            }
        }
        return "object";
    }

    private static int SnifferGetCollectionCount(object value)
    {
        if (value is System.Collections.ICollection coll)
        {
            return coll.Count;
        }
        int count = 0;
        if (value is System.Collections.IEnumerable enumerable)
        {
            foreach (object _ in enumerable)
            {
                count++;
            }
        }
        return count;
    }

    private void SnifferLogComplexMarker(
        string className,
        int instanceId,
        string memberName,
        Type valueType,
        bool snapshot
    )
    {
        SnifferWriteRaw(
            snapshot,
            className,
            "C",
            memberName,
            instanceId,
            valueType.FullName ?? valueType.Name,
            valueType.Assembly.GetName().Name
        );
    }

    private void SnifferLogSimpleValue(
        Dictionary<string, object> store,
        string kind,
        string className,
        string path,
        int instanceId,
        object newValue,
        bool snapshot
    )
    {
        if (snapshot)
        {
            SnifferWriteLine(true, className, kind, path, instanceId, newValue, newValue);
            return;
        }
        string key = $"{className}.{path}#{instanceId}";
        store.TryGetValue(key, out object oldValue);
        if (Equals(oldValue, newValue))
        {
            return;
        }
        store[key] = newValue;
        SnifferWriteLine(false, className, kind, path, instanceId, oldValue, newValue);
    }

    private void SnifferWriteLine(
        bool snapshot,
        string className,
        string kind,
        string path,
        int instanceId,
        object oldValue,
        object newValue
    )
    {
        SnifferWriteRaw(
            snapshot,
            className,
            kind,
            path,
            instanceId,
            FormatSniffValue(oldValue),
            FormatSniffValue(newValue)
        );
    }

    private void SnifferWriteRaw(
        bool snapshot,
        string className,
        string kind,
        string path,
        int instanceId,
        string col6,
        string col7
    )
    {
        if (snifferWriter == null)
        {
            return;
        }
        string prefix = snapshot ? $"{tickCount},SNAPSHOT" : tickCount.ToString();
        snifferWriter.WriteLine($"{prefix},{className},{kind},{path},{instanceId},{col6},{col7}");
    }

    private static string FormatSniffValue(object v)
    {
        if (v == null)
            return "null";
        if (v is IFormattable f)
            return f.ToString(null, System.Globalization.CultureInfo.InvariantCulture);
        return v.ToString();
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
        snifferWriter?.Dispose();
        Logger.LogInfo($"Plugin recorded {tickCount} ticks");
        Logger.LogInfo($"Plugin {MyPluginInfo.PLUGIN_GUID} is unloaded!");
    }
}
