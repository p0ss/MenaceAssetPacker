using System;
using System.Collections.Generic;
using MoonSharp.Interpreter;
using UnityEngine;

namespace Menace.SDK;

/// <summary>
/// Simple animation components for common effects like rotating wheels,
/// hovering aircraft, bobbing objects, etc.
///
/// Console commands:
///   anim.rotate path axis speed    - e.g., anim.rotate "Wheel" up 360
///   anim.hover path amplitude freq - e.g., anim.hover "Ship" 0.5 1
///   anim.bob path amplitude freq   - e.g., anim.bob "Boat" 0.3 0.5
///   anim.stop path                 - remove all animation components
///   anim.list                      - list active animations
///
/// Lua helpers:
///   add_rotate(path, axis, speed)
///   add_hover(path, amplitude, frequency)
///   add_bob(path, bob_amplitude, bob_frequency)
///   remove_animations(path)
///
/// C# usage:
///   var rotator = gameObject.AddComponent<Rotate>();
///   rotator.axis = Vector3.right;
///   rotator.speed = 180f;
/// </summary>
public static class SimpleAnimations
{
    private static readonly List<(GameObject obj, string type)> _activeAnimations = new();

    /// <summary>
    /// Register console commands for animation testing.
    /// </summary>
    public static void RegisterConsoleCommands()
    {
        DevConsole.RegisterCommand("anim.rotate", "Add rotation to object: anim.rotate <path> <axis> <speed>", args =>
        {
            if (args.Length < 3) return "Usage: anim.rotate <path> <axis:up|right|forward> <speed>";
            var go = GameObject.Find(args[0]);
            if (go == null) return $"GameObject not found: {args[0]}";

            var axis = ParseAxis(args[1]);
            if (!float.TryParse(args[2], out float speed)) return "Invalid speed";

            var rotator = go.AddComponent<Rotate>();
            rotator.axis = axis;
            rotator.speed = speed;
            _activeAnimations.Add((go, "Rotate"));
            return $"Added Rotate to {args[0]}: axis={args[1]}, speed={speed}";
        });

        DevConsole.RegisterCommand("anim.hover", "Add hover to object: anim.hover <path> <amplitude> <frequency>", args =>
        {
            if (args.Length < 3) return "Usage: anim.hover <path> <amplitude> <frequency>";
            var go = GameObject.Find(args[0]);
            if (go == null) return $"GameObject not found: {args[0]}";

            if (!float.TryParse(args[1], out float amp)) return "Invalid amplitude";
            if (!float.TryParse(args[2], out float freq)) return "Invalid frequency";

            var hover = go.AddComponent<Hover>();
            hover.amplitude = amp;
            hover.frequency = freq;
            _activeAnimations.Add((go, "Hover"));
            return $"Added Hover to {args[0]}: amplitude={amp}, frequency={freq}";
        });

        DevConsole.RegisterCommand("anim.bob", "Add bob to object: anim.bob <path> <amplitude> <frequency>", args =>
        {
            if (args.Length < 3) return "Usage: anim.bob <path> <amplitude> <frequency>";
            var go = GameObject.Find(args[0]);
            if (go == null) return $"GameObject not found: {args[0]}";

            if (!float.TryParse(args[1], out float amp)) return "Invalid amplitude";
            if (!float.TryParse(args[2], out float freq)) return "Invalid frequency";

            var bob = go.AddComponent<Bob>();
            bob.bobAmplitude = amp;
            bob.bobFrequency = freq;
            _activeAnimations.Add((go, "Bob"));
            return $"Added Bob to {args[0]}: amplitude={amp}, frequency={freq}";
        });

        DevConsole.RegisterCommand("anim.stop", "Remove animations from object: anim.stop <path>", args =>
        {
            if (args.Length < 1) return "Usage: anim.stop <path>";
            var go = GameObject.Find(args[0]);
            if (go == null) return $"GameObject not found: {args[0]}";

            int count = 0;
            foreach (var r in go.GetComponents<Rotate>()) { UnityEngine.Object.Destroy(r); count++; }
            foreach (var h in go.GetComponents<Hover>()) { UnityEngine.Object.Destroy(h); count++; }
            foreach (var b in go.GetComponents<Bob>()) { UnityEngine.Object.Destroy(b); count++; }
            foreach (var s in go.GetComponents<Spin>()) { UnityEngine.Object.Destroy(s); count++; }
            foreach (var o in go.GetComponents<Oscillate>()) { UnityEngine.Object.Destroy(o); count++; }

            _activeAnimations.RemoveAll(a => a.obj == go);
            return $"Removed {count} animation component(s) from {args[0]}";
        });

        DevConsole.RegisterCommand("anim.list", "List active animations", _ =>
        {
            if (_activeAnimations.Count == 0) return "No active animations";
            _activeAnimations.RemoveAll(a => a.obj == null); // Clean up destroyed objects
            var lines = new List<string> { $"Active animations ({_activeAnimations.Count}):" };
            foreach (var (obj, type) in _activeAnimations)
            {
                lines.Add($"  {obj.name}: {type}");
            }
            return string.Join("\n", lines);
        });
    }

    /// <summary>
    /// Register Lua helper functions for animations.
    /// </summary>
    public static void RegisterLuaHelpers(Script script)
    {
        script.Globals["add_rotate"] = DynValue.NewCallback((ctx, args) =>
        {
            var path = args[0].String;
            var axisStr = args.Count > 1 ? args[1].String : "forward";
            var speed = args.Count > 2 ? (float)args[2].Number : 90f;

            var go = GameObject.Find(path);
            if (go == null) return DynValue.NewString($"GameObject not found: {path}");

            var rotator = go.AddComponent<Rotate>();
            rotator.axis = ParseAxis(axisStr);
            rotator.speed = speed;
            _activeAnimations.Add((go, "Rotate"));
            return DynValue.NewString($"Added Rotate to {path}");
        });

        script.Globals["add_hover"] = DynValue.NewCallback((ctx, args) =>
        {
            var path = args[0].String;
            var amplitude = args.Count > 1 ? (float)args[1].Number : 0.5f;
            var frequency = args.Count > 2 ? (float)args[2].Number : 1f;

            var go = GameObject.Find(path);
            if (go == null) return DynValue.NewString($"GameObject not found: {path}");

            var hover = go.AddComponent<Hover>();
            hover.amplitude = amplitude;
            hover.frequency = frequency;
            _activeAnimations.Add((go, "Hover"));
            return DynValue.NewString($"Added Hover to {path}");
        });

        script.Globals["add_bob"] = DynValue.NewCallback((ctx, args) =>
        {
            var path = args[0].String;
            var amplitude = args.Count > 1 ? (float)args[1].Number : 0.3f;
            var frequency = args.Count > 2 ? (float)args[2].Number : 0.5f;

            var go = GameObject.Find(path);
            if (go == null) return DynValue.NewString($"GameObject not found: {path}");

            var bob = go.AddComponent<Bob>();
            bob.bobAmplitude = amplitude;
            bob.bobFrequency = frequency;
            _activeAnimations.Add((go, "Bob"));
            return DynValue.NewString($"Added Bob to {path}");
        });

        script.Globals["remove_animations"] = DynValue.NewCallback((ctx, args) =>
        {
            var path = args[0].String;
            var go = GameObject.Find(path);
            if (go == null) return DynValue.NewString($"GameObject not found: {path}");

            int count = 0;
            foreach (var r in go.GetComponents<Rotate>()) { UnityEngine.Object.Destroy(r); count++; }
            foreach (var h in go.GetComponents<Hover>()) { UnityEngine.Object.Destroy(h); count++; }
            foreach (var b in go.GetComponents<Bob>()) { UnityEngine.Object.Destroy(b); count++; }
            foreach (var s in go.GetComponents<Spin>()) { UnityEngine.Object.Destroy(s); count++; }
            foreach (var o in go.GetComponents<Oscillate>()) { UnityEngine.Object.Destroy(o); count++; }

            _activeAnimations.RemoveAll(a => a.obj == go);
            return DynValue.NewString($"Removed {count} animations from {path}");
        });
    }

    private static Vector3 ParseAxis(string axis)
    {
        return axis.ToLower() switch
        {
            "up" or "y" => Vector3.up,
            "right" or "x" => Vector3.right,
            "forward" or "z" => Vector3.forward,
            "down" => Vector3.down,
            "left" => Vector3.left,
            "back" => Vector3.back,
            _ => Vector3.forward
        };
    }
}

/// <summary>
/// Continuously rotates a transform around an axis.
/// Useful for wheels, propellers, fans, radar dishes, etc.
/// </summary>
public class Rotate : MonoBehaviour
{
    /// <summary>
    /// Rotation axis in local space. Default is forward (Z axis).
    /// Common values: Vector3.forward (propeller), Vector3.right (wheel), Vector3.up (turret)
    /// </summary>
    public Vector3 axis = Vector3.forward;

    /// <summary>
    /// Rotation speed in degrees per second.
    /// Positive = counter-clockwise when looking along axis.
    /// </summary>
    public float speed = 90f;

    /// <summary>
    /// Whether to use unscaled time (ignores game pause/slow-mo).
    /// </summary>
    public bool unscaledTime = false;

    void Update()
    {
        float dt = unscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
        transform.Rotate(axis, speed * dt, Space.Self);
    }
}

/// <summary>
/// Makes a transform hover up and down using a sine wave.
/// Useful for floating/flying objects, magic items, etc.
/// </summary>
public class Hover : MonoBehaviour
{
    /// <summary>
    /// Maximum displacement from starting position.
    /// </summary>
    public float amplitude = 0.5f;

    /// <summary>
    /// Oscillation frequency (cycles per second).
    /// </summary>
    public float frequency = 1f;

    /// <summary>
    /// Axis of movement in local space. Default is up (Y axis).
    /// </summary>
    public Vector3 axis = Vector3.up;

    /// <summary>
    /// Phase offset in radians (0 to 2*PI). Use to desync multiple objects.
    /// </summary>
    public float phase = 0f;

    private Vector3 _startPosition;

    void Start()
    {
        _startPosition = transform.localPosition;
    }

    void Update()
    {
        float offset = Mathf.Sin((Time.time * frequency * 2f * Mathf.PI) + phase) * amplitude;
        transform.localPosition = _startPosition + axis.normalized * offset;
    }
}

/// <summary>
/// Bobs a transform up and down with optional rotation.
/// Similar to Hover but with additional tilt for a more organic feel.
/// Useful for boats, floating debris, breathing creatures, etc.
/// </summary>
public class Bob : MonoBehaviour
{
    /// <summary>
    /// Vertical bob amplitude.
    /// </summary>
    public float bobAmplitude = 0.3f;

    /// <summary>
    /// Bob frequency (cycles per second).
    /// </summary>
    public float bobFrequency = 0.5f;

    /// <summary>
    /// Maximum tilt angle in degrees.
    /// </summary>
    public float tiltAngle = 5f;

    /// <summary>
    /// Tilt frequency (cycles per second). Slightly different from bob creates organic feel.
    /// </summary>
    public float tiltFrequency = 0.3f;

    /// <summary>
    /// Tilt axis in local space.
    /// </summary>
    public Vector3 tiltAxis = Vector3.right;

    private Vector3 _startPosition;
    private Quaternion _startRotation;

    void Start()
    {
        _startPosition = transform.localPosition;
        _startRotation = transform.localRotation;
    }

    void Update()
    {
        // Vertical bob
        float bobOffset = Mathf.Sin(Time.time * bobFrequency * 2f * Mathf.PI) * bobAmplitude;
        transform.localPosition = _startPosition + Vector3.up * bobOffset;

        // Tilt
        float tilt = Mathf.Sin(Time.time * tiltFrequency * 2f * Mathf.PI) * tiltAngle;
        transform.localRotation = _startRotation * Quaternion.AngleAxis(tilt, tiltAxis);
    }
}

/// <summary>
/// Spins a transform with acceleration/deceleration.
/// Useful for objects that spin up/down like turbines, centrifuges, etc.
/// </summary>
public class Spin : MonoBehaviour
{
    /// <summary>
    /// Target rotation speed in degrees per second.
    /// </summary>
    public float targetSpeed = 360f;

    /// <summary>
    /// Current rotation speed (read-only, or set for initial speed).
    /// </summary>
    public float currentSpeed = 0f;

    /// <summary>
    /// Acceleration in degrees per second squared.
    /// </summary>
    public float acceleration = 90f;

    /// <summary>
    /// Rotation axis in local space.
    /// </summary>
    public Vector3 axis = Vector3.up;

    /// <summary>
    /// Whether the spin is active. Set to false to spin down.
    /// </summary>
    public bool active = true;

    void Update()
    {
        float target = active ? targetSpeed : 0f;

        if (currentSpeed < target)
        {
            currentSpeed = Mathf.Min(currentSpeed + acceleration * Time.deltaTime, target);
        }
        else if (currentSpeed > target)
        {
            currentSpeed = Mathf.Max(currentSpeed - acceleration * Time.deltaTime, target);
        }

        if (Mathf.Abs(currentSpeed) > 0.01f)
        {
            transform.Rotate(axis, currentSpeed * Time.deltaTime, Space.Self);
        }
    }
}

/// <summary>
/// Oscillates a transform between two positions or rotations.
/// Useful for pendulums, swinging doors, scanning sensors, etc.
/// </summary>
public class Oscillate : MonoBehaviour
{
    public enum OscillateMode
    {
        Position,
        Rotation
    }

    /// <summary>
    /// Whether to oscillate position or rotation.
    /// </summary>
    public OscillateMode mode = OscillateMode.Position;

    /// <summary>
    /// For Position mode: offset from start position at max displacement.
    /// For Rotation mode: axis of rotation.
    /// </summary>
    public Vector3 axis = Vector3.right;

    /// <summary>
    /// For Position mode: max displacement distance.
    /// For Rotation mode: max angle in degrees.
    /// </summary>
    public float magnitude = 1f;

    /// <summary>
    /// Oscillation frequency (cycles per second).
    /// </summary>
    public float frequency = 1f;

    /// <summary>
    /// Phase offset in radians.
    /// </summary>
    public float phase = 0f;

    private Vector3 _startPosition;
    private Quaternion _startRotation;

    void Start()
    {
        _startPosition = transform.localPosition;
        _startRotation = transform.localRotation;
    }

    void Update()
    {
        float t = Mathf.Sin((Time.time * frequency * 2f * Mathf.PI) + phase);

        if (mode == OscillateMode.Position)
        {
            transform.localPosition = _startPosition + axis.normalized * (t * magnitude);
        }
        else
        {
            transform.localRotation = _startRotation * Quaternion.AngleAxis(t * magnitude, axis);
        }
    }
}
