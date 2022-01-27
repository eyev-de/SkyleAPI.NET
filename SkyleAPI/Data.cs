using System;

namespace Skyle
{
    /// <summary>
    /// Available Button Actions:
    /// "none", "unknown", "leftClick", "rightClick", "scroll", "calibrate", pause
    /// </summary>
    public enum ButtonAction
    {
        none,
        unknown,
        leftClick,
        rightClick,
        scroll,
        calibrate,
        pause
    }

    /// <summary>
    /// Button configuration class
    /// </summary>
    public class Button
    {
        internal Button(bool isPresent)
        {
            this.isPresent = isPresent;
        }
        /// <summary>
        /// Indicator if a button is connected
        /// </summary>
        public bool isPresent { get; }
        /// <summary>
        /// Action to trigger, when a single click on the button is performed
        /// </summary>
        public ButtonAction SingleClick { get; set; }
        /// <summary>
        /// Action to trigger, when a double click on the button is performed
        /// </summary>
        public ButtonAction DoubleClick { get; set; }
        /// <summary>
        /// Action to trigger when the button is constantly pushed
        /// </summary>
        public ButtonAction HoldClick { get; set; }
    }

    /// <summary>
    /// Positioning Data
    /// </summary>
    public class Positioning
    {
        internal Positioning(Skyle_Server.PositioningMessage p)
        {
            LeftEye = new Point(p.LeftEye);
            RightEye = new Point(p.RightEye);
            QualityDepth = p.QualityDepth;
            QualitySides = p.QualitySides;
            QualityXAxis = p.QualityXAxis;
            QualityYAxis = p.QualityYAxis;
        }
        /// <summary>
        /// Point of left eye
        /// </summary>
        public Point LeftEye { get; }
        /// <summary>
        /// Point of left eye
        /// </summary>
        public Point RightEye { get; }
        /// <summary>
        /// Quality indicator for depth positioning. range is -50 to +50. 0 is the best, -50 to far away and 50 to close
        /// </summary>
        public int QualityDepth { get; }
        /// <summary>
        /// Quality indicator for overall horizontal and vertical positioning : range is -50 to +50. 0 is the best
        /// </summary>
        public int QualitySides { get; }
        /// <summary>
        /// Quality indicator for horizontal positioning. range is -50 to +50. 0 is the best, -50 to far left and 50 to far right
        /// </summary>
        public int QualityXAxis { get; }
        /// <summary>
        /// Quality indicator for vertical positioning. range is -50 to +50. 0 is the best, -50 to far down and 50 to far up
        /// </summary>
        public int QualityYAxis { get; }
    }

    /// <summary>
    /// Skyle Type Enum
    /// </summary>
    public enum SkyleType
    {
        Unknown = 0,
        /// <summary>
        /// Device for general usage with SDK or Windows (no HID movement)
        /// </summary>
        General = 1,
        /// <summary>
        /// Device for usage with iPad Pro 13, second version
        /// </summary>
        iPadProV2 = 2,
        /// <summary>
        /// Device for usage with iPad Pro 13
        /// </summary>
        iPadPro = 4,
        /// <summary>
        /// Custom device
        /// </summary>
        Custom = 5
    }

    /// <summary>
    /// Firmware versions and device specific data
    /// </summary>
    public class DeviceVersions
    {
        internal DeviceVersions(Skyle_Server.DeviceVersions v)
        {
            Firmware = v.Firmware;
            Eyetracking = v.Eyetracker;
            Calibration = v.Calib;
            Base = v.Base;
            Serial = v.Serial;
            isDemoDevice = v.IsDemo;
            SkyleType = (SkyleType)v.SkyleType;
        }
        /// <summary>
        /// General firmware version
        /// </summary>
        public string Firmware { get; }
        /// <summary>
        /// Version of eyetracking component
        /// </summary>
        public string Eyetracking { get; }
        /// <summary>
        /// Version of calibration component
        /// </summary>
        public string Calibration { get; }
        /// <summary>
        /// Version of base system
        /// </summary>
        public string Base { get; }
        /// <summary>
        /// Serial of this device
        /// </summary>
        public ulong Serial { get; }
        /// <summary>
        /// Indicator for Demo Device
        /// </summary>
        public bool isDemoDevice { get; }
        /// <summary>
        /// Skyle Type
        /// </summary>
        public SkyleType SkyleType { get; }
    }

    public class Trigger
    {
        /// <summary>
        /// Trigger Ctor
        /// </summary>
        /// <param name="tm"></param>
        internal Trigger(Skyle_Server.TriggerMessage tm)
        {
            singleClick = tm.SingleClick;
            doubleClick = tm.DoubleClick;
            holdClick = tm.HoldClick;
            fixation = tm.Fixation;
        }

        /// <summary>
        /// Indicator for single click on attached button
        /// </summary>
        public bool singleClick { get; }

        /// <summary>
        /// Indicator for double click on attached button
        /// </summary>
        public bool doubleClick { get; }

        /// <summary>
        /// Indicator that attached button is constantly pushed
        /// </summary>
        public bool holdClick { get; }

        /// <summary>
        /// Indicator that a user is fixating a point
        /// </summary>
        public bool fixation { get; }
    }

    /// <summary>
    /// Static converter class
    /// </summary>
    public static class Convert
    {
        /// <summary>
        /// Button action parser from string
        /// </summary>
        /// <param name="input"></param>
        /// <returns></returns>
        public static ButtonAction fromString(this string input)
        {
            try
            {
                ButtonAction ba = (ButtonAction)Enum.Parse(typeof(ButtonAction), input);
                return Enum.IsDefined(typeof(ButtonAction), ba) ? ba : ButtonAction.unknown;
            }
            catch (Exception)
            {
                return ButtonAction.unknown;
            }
        }

        /// <summary>
        /// Convert ButtonAction to string
        /// </summary>
        /// <param name="input"></param>
        /// <returns></returns>
        public static string toString(this ButtonAction input)
        {
            return input switch
            {
                ButtonAction.unknown => "unknown",
                ButtonAction.none => "none",
                ButtonAction.leftClick => "leftClick",
                ButtonAction.rightClick => "rightClick",
                ButtonAction.scroll => "scroll",
                ButtonAction.calibrate => "calibrate",
                ButtonAction.pause => "pause",
                _ => "unknown",
            };
        }
    }

    /// <summary>
    /// Public Profile
    /// </summary>
    public class Profile
    {
        /// <summary>
        /// Type of profile / skill
        /// </summary>
        public enum Type
        {
            Low, Medium, High
        }

        /// <summary>
        /// ID of the profile
        /// </summary>
        public int ID;

        /// <summary>
        /// Name of the profile
        /// </summary>
        public string Name;

        /// <summary>
        /// Skill of the profile
        /// </summary>
        public Type Skill;

        internal Profile(Skyle_Server.Profile profile)
        {
            ID = profile.ID;
            Name = profile.Name;
            Skill = profile.Skill.ToProfileSkill();
        }

        /// <summary>
        /// Default constructor
        /// </summary>
        public Profile(string name, Type skill)
        {
            ID = -1;
            Name = name;
            Skill = skill;
        }

        /// <summary>
        /// Converts Profile to the Skyle_Server version
        /// </summary>
        /// <returns>Skyle_Server.Profile</returns>
        internal Skyle_Server.Profile ToProfile()
        {
            return new Skyle_Server.Profile()
            {
                ID = ID,
                Name = Name,
                Skill = Skill.ToProfileSkill(),
            };
        }
    }

    /// <summary>
    /// Converter extension from Skyle_Server.Profile.Types.Skill to Profile.Type
    /// </summary>
    internal static class ProfileTypeExtension
    {
        internal static Profile.Type ToProfileSkill(this Skyle_Server.Profile.Types.Skill value)
        {
            return value switch
            {
                Skyle_Server.Profile.Types.Skill.Low => Profile.Type.Low,
                Skyle_Server.Profile.Types.Skill.Medium => Profile.Type.Medium,
                Skyle_Server.Profile.Types.Skill.High => Profile.Type.High,
                _ => throw new Exception(),
            };
        }
    }

    /// <summary>
    /// Converter extension from Profile.Type to Skyle_Server.Profile.Types.Skill
    /// </summary>
    internal static class Skyle_ServerProfileSkillExtension
    {
        internal static Skyle_Server.Profile.Types.Skill ToProfileSkill(this Profile.Type value)
        {
            return value switch
            {
                Profile.Type.Low => Skyle_Server.Profile.Types.Skill.Low,
                Profile.Type.Medium => Skyle_Server.Profile.Types.Skill.Medium,
                Profile.Type.High => Skyle_Server.Profile.Types.Skill.High,
                _ => throw new Exception(),
            };
        }
    }
}
