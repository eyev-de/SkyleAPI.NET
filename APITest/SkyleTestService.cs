using System;
using Skyle_Server;
using Grpc.Core;
using Google.Protobuf.WellKnownTypes;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Linq;

namespace APITest
{
    internal class SkyleTestService : Skyle_Server.Skyle.SkyleBase
    {
        internal bool stream = false;

        private readonly List<Profile> profiles = new List<Profile>() {
            new Profile() {
                ID = 0,
                Name = "Default",
                Skill = Profile.Types.Skill.Medium,
            },
        };

        private Profile currentProfile = new Profile()
        {
            ID = 0,
            Name = "Default",
            Skill = Profile.Types.Skill.Medium,
        };


        public override Task<StatusMessage> SetProfile(Profile request, ServerCallContext context)
        {
            if (request.ID != -1)
            {
                var profile = profiles.Where((profile) => profile.ID == request.ID).First();
                profiles.Remove(profile);
            }
            profiles.Add(request);
            currentProfile = request;
            var res = new StatusMessage() { Success = true };
            return Task.FromResult(res);
        }

        public override async Task GetProfiles(Empty request, IServerStreamWriter<Profile> responseStream, ServerCallContext context)
        {
            foreach (var profile in profiles)
            {
                await responseStream.WriteAsync(profile);
            }
        }

        public override Task<Profile> CurrentProfile(Empty request, ServerCallContext context)
        {
            return Task.FromResult(currentProfile);
        }

        public override Task<StatusMessage> DeleteProfile(Profile request, ServerCallContext context)
        {
            if (request.ID != 0)
            {
                var profile = profiles.Where((profile) => profile.ID == request.ID).First();
                profiles.Remove(profile);
                currentProfile = profiles.Where((profile) => profile.ID == 0).First();
            }
            var res = new StatusMessage() { Success = true };
            return Task.FromResult(res);
        }

        public override Task<Skyle_Server.Options> Configure(Skyle_Server.OptionMessage request, ServerCallContext context)
        {
            if (request.MessageCase == OptionMessage.MessageOneofCase.Options)
            {
                stream = request.Options.Stream;
            }
            Skyle_Server.Options result = new Skyle_Server.Options()
            {
                Stream = stream
            };
            return Task.FromResult(result);
        }

        /// <summary>
        /// Fake Version Get
        /// </summary>
        /// <param name="request"></param>
        /// <param name="context"></param>
        /// <returns></returns>
        public override Task<DeviceVersions> GetVersions(Empty request, ServerCallContext context)
        {
            DeviceVersions dv = new DeviceVersions()
            {
                Firmware = "v0.0.test",
                Eyetracker = "v0.0.test",
                Calib = "v0.0.test",
                Base = "v0.0.test"
            };
            return Task.FromResult(dv);
        }
    }
}
