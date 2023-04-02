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
    private readonly EffectInfo constantForce;
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

        foreach (var effectInfo in wheel.GetEffects())
            Debug.WriteLine("Effect available: " + effectInfo.Name);

        wheel.SendForceFeedbackCommand(ForceFeedbackCommand.SetActuatorsOn);

        constantForce = wheel.GetEffects().FirstOrDefault(x => x.Guid == EffectGuid.ConstantForce);
        wheelActuator = wheel.GetObjects().First(doi => doi.ObjectId.Flags.HasFlag(DeviceObjectTypeFlags.ForceFeedbackActuator));
        axes = new[] { (int)wheelActuator.ObjectId };
    }

    private const double AxisCenter = (short.MaxValue + 0.5);

    // Joystick Axes
    public double SteeringAxis { get; private set; }
    public double ThrottleAxis { get; private set; }
    public double BrakeAxis { get; private set; }
    public double ClutchAxis { get; private set; }

    public double FeedbackMagnitude { get; private set; }

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

    private Effect lastEffect;
    public void PlayConstantForce(double magnitude)
    {
        FeedbackMagnitude = magnitude;
        try
        {
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
            effectParams.SetAxes(axes, new int[] { 0 });
            var cf = new ConstantForce
            {
                Magnitude = (int)(wheel.Properties.ForceFeedbackGain * magnitude)
            };
            effectParams.Parameters = cf;

            var effect = new Effect(wheel, constantForce.Guid, effectParams);
            lastEffect?.Dispose();
            effect.Start();
            lastEffect = effect;
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
