using SharpDX.DirectInput;
using System;
using System.Diagnostics;
using System.Linq;
using Microsoft.UI.Xaml;

namespace ForceFeedbackRelay;

internal class JoystickNotConnectedException : Exception
{
    public JoystickNotConnectedException() : base("No Joystick Connected") {}
    public JoystickNotConnectedException(Exception inner) : base("No Joystick Connected", inner) {}
}

internal class WheelController
{
    private readonly Joystick wheel;
    private readonly DeviceObjectInstance wheelActuator;
    private readonly int[] axes;

    public WheelController(Window window)
    {
        // Initialize Joystick
        DirectInput directInput = new DirectInput();
        Guid joystickGuid = Guid.Empty;
        foreach (var deviceInstance in directInput.GetDevices(DeviceType.Driving, DeviceEnumerationFlags.ForceFeedback))
        {
            Debug.WriteLine("Found device: " + deviceInstance.InstanceName);
            joystickGuid = deviceInstance.InstanceGuid;
        }

        if (joystickGuid == Guid.Empty)
        {
            throw new JoystickNotConnectedException();
        }

        wheel = new Joystick(directInput, joystickGuid);

        // Configure wheel for Force Feedback
        var hWnd = WinRT.Interop.WindowNative.GetWindowHandle(window);
        wheel.SetCooperativeLevel(hWnd, CooperativeLevel.Exclusive | CooperativeLevel.Background);
        wheel.Properties.BufferSize = 128;

        // Acquire wheel
        wheel.Acquire();

        wheel.SendForceFeedbackCommand(ForceFeedbackCommand.SetActuatorsOn);

        // Store Effect info for in use effects
        foreach (var effect in wheel.GetEffects())
        {
            Debug.WriteLine("Effect Name: " + effect.Name);
            Debug.WriteLine("Effect Dynamic Parameters: " + effect.DynamicParameters);
            Debug.WriteLine("Effect Type: " + effect.Type + "\n");
        }

        wheelActuator = wheel.GetObjects()
            .First(doi => doi.ObjectId.Flags.HasFlag(DeviceObjectTypeFlags.ForceFeedbackActuator));
        axes = new[] { (int)wheelActuator.ObjectId };
    }

    private const double AxisCenter = (short.MaxValue + 0.5);

    // Joystick Axes
    public double SteeringAxis { get; private set; }
    public double ThrottleAxis { get; private set; }
    public double BrakeAxis { get; private set; }
    public double ClutchAxis { get; private set; }

    public void Poll()
    {
        if (wheel == null) return;
        try
        {
            wheel.Poll();
            var data = wheel.GetBufferedData();
            foreach (var update in data)
            {
                var value = (update.Value - AxisCenter) / AxisCenter;
                switch (update.Offset)
                {
                    case JoystickOffset.X:
                        SteeringAxis = value;
                        break;
                    case JoystickOffset.Y:
                        ThrottleAxis = value;
                        break;
                    case JoystickOffset.RotationZ:
                        BrakeAxis = value;
                        break;
                    case JoystickOffset.Sliders0:
                        ClutchAxis = value;
                        break;
                }
            }
        }
        catch (Exception e)
        {
            throw new JoystickNotConnectedException(e);
        }
    }

    public double ConstantForceMagnitude { get; private set; }
    private Effect constantForceEffect;

    public void PlayConstantForce(double magnitude)
    {
        // If we're already playing this effect, don't set again
        if (magnitude == ConstantForceMagnitude) return;

        ConstantForceMagnitude = magnitude;

        var effectParams = new EffectParameters
        {
            Flags = EffectFlags.Cartesian | EffectFlags.ObjectIds,
            StartDelay = 0,
            SamplePeriod = wheel.Capabilities.ForceFeedbackSamplePeriod,
            Duration = int.MaxValue,
            TriggerButton = -1,
            TriggerRepeatInterval = int.MaxValue,
            Gain = wheel.Properties.ForceFeedbackGain
        };
        effectParams.SetAxes(axes, new[] { 0 });
        effectParams.Parameters = new ConstantForce
        {
            Magnitude = (int)(wheel.Properties.ForceFeedbackGain * -magnitude)
        };

        try
        {

            var effect = new Effect(wheel, EffectGuid.ConstantForce, effectParams);
            constantForceEffect?.Dispose();
            effect.Start();
            constantForceEffect = effect;
        }
        catch (Exception e)
        {
            throw new JoystickNotConnectedException(e);
        }
    }

    public double DamperNegativeResistance { get; private set; }
    public double DamperPositiveResistance { get; private set; }
    public double DamperConstantForce { get; private set; }
    public double DamperDeadBand { get; private set; }
    private Effect damperEffect;

    // TODO proper documentation
    // positiveResistance is the resistance when turning the wheel right, negativeResistance is resistance turning left
    // deadBand is the speed the wheel must be turning for the resistance to kick in
    // 
    public void PlayDamperForce(double negativeResistance, double positiveResistance, double constantForce, double deadBand)
    {
        // If we're already playing this effect, don't set again
        if (negativeResistance == DamperNegativeResistance && 
            positiveResistance == DamperPositiveResistance && 
            constantForce == DamperConstantForce && 
            deadBand == DamperDeadBand) return;

        DamperNegativeResistance = negativeResistance;
        DamperPositiveResistance = positiveResistance;
        DamperConstantForce = constantForce;
        DamperDeadBand = deadBand;

        var effectParams = new EffectParameters
        {
            Flags = EffectFlags.Cartesian | EffectFlags.ObjectIds,
            StartDelay = 0,
            SamplePeriod = wheel.Capabilities.ForceFeedbackSamplePeriod,
            Duration = int.MaxValue,
            TriggerButton = -1,
            Gain = wheel.Properties.ForceFeedbackGain
        };
        effectParams.SetAxes(axes, new[] { 0 });

        var conditions = new Condition
        {
            Offset = (int)(wheel.Properties.ForceFeedbackGain * constantForce),
            NegativeSaturation = (int)(wheel.Properties.ForceFeedbackGain * negativeResistance),
            PositiveSaturation = (int)(wheel.Properties.ForceFeedbackGain * positiveResistance),
            NegativeCoefficient = (int)(wheel.Properties.ForceFeedbackGain * negativeResistance),
            PositiveCoefficient = (int)(wheel.Properties.ForceFeedbackGain * positiveResistance),
            DeadBand = (int)(wheel.Properties.ForceFeedbackGain * deadBand)
        };
        effectParams.Parameters = new ConditionSet
        {
            Conditions = new[] { conditions }
        };

        try
        {
            var effect = new Effect(wheel, EffectGuid.Damper, effectParams);
            damperEffect?.Dispose();
            effect.Start();
            damperEffect = effect;
        }
        catch (Exception e)
        {
            throw new JoystickNotConnectedException(e);
        }
    }

    public double SpringNegativeSaturation { get; private set; }
    public double SpringPositiveSaturation { get; private set; }
    public double SpringNegativeGain { get; private set; }
    public double SpringPositiveGain { get; private set; }
    public double SpringCenterPoint { get; private set; }
    public double SpringDeadBand { get; private set; }
    private Effect springEffect;

    // TODO proper documentation
    // negative and positive saturation is the maximum force applied to center
    // negative and positive gain is the P term of the PID
    // center point is the PID set point
    // deadBand is unkown
    // 
    public void PlaySpringForce(double negativeSaturation, double positiveSaturation, double negativeGain, double positiveGain, double centerPoint, double deadBand)
    {
        // If we're already playing this effect, don't set again
        if (negativeSaturation == SpringNegativeSaturation &&
            positiveSaturation == SpringPositiveSaturation &&
            negativeGain == SpringNegativeGain &&
            positiveGain == SpringPositiveGain &&
            centerPoint == SpringCenterPoint &&
            deadBand == SpringDeadBand) return;

        SpringNegativeSaturation = negativeSaturation;
        SpringPositiveSaturation = positiveSaturation;
        SpringNegativeGain = negativeGain;
        SpringPositiveGain = positiveGain;
        SpringCenterPoint = centerPoint;
        SpringDeadBand = deadBand;

        var effectParams = new EffectParameters
        {
            Flags = EffectFlags.Cartesian | EffectFlags.ObjectIds,
            StartDelay = 0,
            SamplePeriod = wheel.Capabilities.ForceFeedbackSamplePeriod,
            Duration = int.MaxValue,
            TriggerButton = -1,
            Gain = wheel.Properties.ForceFeedbackGain
        };
        effectParams.SetAxes(axes, new[] { 0 });

        var conditions = new Condition
        {
            Offset = (int)(wheel.Properties.ForceFeedbackGain * centerPoint),
            NegativeSaturation = (int)(wheel.Properties.ForceFeedbackGain * negativeSaturation),
            PositiveSaturation = (int)(wheel.Properties.ForceFeedbackGain * positiveSaturation),
            NegativeCoefficient = (int)(wheel.Properties.ForceFeedbackGain * negativeGain),
            PositiveCoefficient = (int)(wheel.Properties.ForceFeedbackGain * positiveGain),
            DeadBand = (int)(wheel.Properties.ForceFeedbackGain * deadBand)
        };
        effectParams.Parameters = new ConditionSet
        {
            Conditions = new[] { conditions }
        };

        try
        {
            var effect = new Effect(wheel, EffectGuid.Spring, effectParams);
            springEffect?.Dispose();
            effect.Start();
            springEffect = effect;
        }
        catch (Exception e)
        {
            throw new JoystickNotConnectedException(e);
        }
    }

    public void Dispose()
    {
        wheel.Dispose();
    }
}
