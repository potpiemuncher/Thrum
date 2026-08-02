using DS4Windows;
using DS4WinWPF.DS4Forms.ViewModels;

namespace DS4WindowsTests
{
    [TestClass]
    public class ControllerRumblePulseTests
    {
        [TestMethod]
        public void PulseRestoresThePriorComposedRumbleState()
        {
            var prior = new DS4ForceFeedbackState
            {
                RumbleMotorStrengthRightLightFast = 14,
                RumbleMotorStrengthLeftHeavySlow = 27,
            };
            var target = new FakeTransientRumbleTarget(prior);

            ControllerRumblePulse pulse = ControllerRumblePulse.Begin(target);

            Assert.AreNotEqual(prior, target.State);
            Assert.IsTrue(target.State.IsRumbleSet());
            Assert.IsTrue(pulse.Restore());
            Assert.AreEqual(prior, target.State);
            Assert.IsFalse(pulse.Restore(),
                "A completed lease must be idempotent.");
        }

        [TestMethod]
        public void PulseDoesNotOverwriteNewerRumbleFeedback()
        {
            var target = new FakeTransientRumbleTarget(default);
            ControllerRumblePulse pulse = ControllerRumblePulse.Begin(target);
            var newer = new DS4ForceFeedbackState
            {
                RumbleMotorStrengthRightLightFast = 201,
                RumbleMotorStrengthLeftHeavySlow = 202,
            };

            target.ApplyNewer(newer);

            Assert.IsFalse(pulse.Restore());
            Assert.AreEqual(newer, target.State);
        }

        [TestMethod]
        public void ZeroPriorStateProducesAnExplicitBoundedStop()
        {
            DS4ForceFeedbackState restored =
                ControllerTransientRumblePolicy.PrepareRestoreState(default);

            Assert.AreEqual((byte)0,
                restored.RumbleMotorStrengthRightLightFast);
            Assert.AreEqual((byte)0,
                restored.RumbleMotorStrengthLeftHeavySlow);
            Assert.IsTrue(restored.RumbleMotorsExplicitlyOff,
                "A logical-zero prior state still needs one stop report.");
        }

        private sealed class FakeTransientRumbleTarget :
            IControllerTransientRumbleTarget
        {
            private long revision;

            internal FakeTransientRumbleTarget(DS4ForceFeedbackState state)
            {
                State = state;
            }

            internal DS4ForceFeedbackState State { get; private set; }

            public ControllerTransientRumbleLeaseState BeginTransientRumble(
                byte rightLightFastMotor, byte leftHeavySlowMotor)
            {
                DS4ForceFeedbackState previous = State;
                State = new DS4ForceFeedbackState
                {
                    RumbleMotorStrengthRightLightFast = rightLightFastMotor,
                    RumbleMotorStrengthLeftHeavySlow = leftHeavySlowMotor,
                };
                revision++;
                return new ControllerTransientRumbleLeaseState(previous,
                    revision);
            }

            public bool RestoreTransientRumble(
                ControllerTransientRumbleLeaseState lease)
            {
                if (lease.Revision != revision) return false;
                State = lease.PreviousState;
                revision++;
                return true;
            }

            internal void ApplyNewer(DS4ForceFeedbackState state)
            {
                State = state;
                revision++;
            }
        }
    }
}
