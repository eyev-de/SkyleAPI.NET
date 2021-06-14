using Grpc.Core;
using Skyle_Server;
using System;
using System.Threading.Tasks;
using Xunit;
using Skyle;
using System.Diagnostics;

namespace APITest
{
    public class UnitTest1
    {
        private Server server;
        private SkyleTestService service;

        [Fact]
        public void TestOptionStream()
        {
            service = new SkyleTestService();
            StartServer();
            Client c = new Client("localhost");
            Assert.True(c.ChangeStream(true));
            Assert.False(c.ChangeStream(false));
            StopServer();
        }

        [Fact]
        public async Task TestAvailableAsync()
        {
            service = new SkyleTestService();
            StartServer();
            Client c = new Client("localhost");
            bool con = await c.ConnectAsync();
            Assert.True(con && c.Available());
            //StopServer();
            await server.KillAsync();
            Assert.False(c.Available());
            //StopServer();
        }

        [Fact]
        public async Task TestAvailableRemoteAsync()
        {
            Client c = new Client();
            bool con = await c.ConnectAsync();
            if (con)
            {
                c.GetVersions();
                Assert.True(c.Available());
                await Task.Delay(5000);
                //disconnect ET manually
            }
            Assert.False(c.Available());
        }

        [Fact]
        public async Task TestAvailableTimeout()
        {
            int count = 0, target = 3;
            service = new SkyleTestService();
            _ = Task.Run(async () =>
               {
                   await Task.Delay(3500 * target);
                   StartServer();
               });
            Client c = new Client("localhost");
            while (!await c.ConnectAsync())
            {
                //waiting
                count++;
            }
            Assert.Equal(count, target);
            StopServer();
        }

        [Fact]
        public async void TestGettingProfiles()
        {
            service = new SkyleTestService();
            StartServer();
            Client c = new Client("localhost");
            await c.ConnectAsync();
            var profiles = await c.GetProfiles();
            Assert.True(profiles.Count > 0);
            foreach (var profile in profiles)
            {
                Trace.WriteLine(profile.ID);
                Trace.WriteLine(profile.Name);
            }
            StopServer();
        }

        [Fact]
        public async void TestGettingCurrentProfile()
        {
            service = new SkyleTestService();
            StartServer();
            Client c = new Client("localhost");
            await c.ConnectAsync();
            var profile = c.GetCurrentProfile();
            Trace.WriteLine(profile.Name);
            Assert.True(profile.Name == "Default");
            StopServer();
        }

        [Fact]
        public async void TestSettingCurrentProfile()
        {
            service = new SkyleTestService();
            StartServer();
            Client c = new Client("localhost");
            await c.ConnectAsync();
            var profile = c.GetCurrentProfile();

            profile.Skill = Skyle.Profile.Type.Low;
            c.SetProfile(profile);
            profile = c.GetCurrentProfile();
            Assert.True(profile.Skill == Skyle.Profile.Type.Low);

            profile.Skill = Skyle.Profile.Type.Medium;
            c.SetProfile(profile);
            profile = c.GetCurrentProfile();
            Assert.True(profile.Skill == Skyle.Profile.Type.Medium);

            profile.Skill = Skyle.Profile.Type.High;
            c.SetProfile(profile);
            profile = c.GetCurrentProfile();
            Assert.True(profile.Skill == Skyle.Profile.Type.High);
            StopServer();
        }

        [Fact]
        public async void TestSettingNewProfileDeletingNewProfile()
        {
            service = new SkyleTestService();
            StartServer();
            Client c = new Client("localhost");
            await c.ConnectAsync();
            var profile = new Skyle.Profile("Test", Skyle.Profile.Type.Low);
            c.SetProfile(profile);
            profile = c.GetCurrentProfile();

            Trace.WriteLine(profile.Name);
            Trace.WriteLine(profile.ID);

            Assert.True(profile.Skill == Skyle.Profile.Type.Low);
            Assert.True(profile.Name == "Test");

            var res = c.DeleteProfile(profile);
            Assert.True(res);

            profile = c.GetCurrentProfile();
            Trace.WriteLine(profile.Name);
            Assert.True(profile.Name == "Default");
            StopServer();
        }


        private void StartServer(int Port = 50052)
        {
            try
            {
                server = new Server
                {
                    Services = { Skyle_Server.Skyle.BindService(service) },
                    Ports = { new ServerPort("localhost", Port, ServerCredentials.Insecure) }
                };
                server.Start();
                //Logger.Info("gRPC Services listening on port " + Port);           
            }
            catch (Exception)
            {
                //Logger.Error(ex);
                StopServer();
            }
        }

        private void StopServer()
        {
            server?.ShutdownAsync().Wait(1000);
        }
    }
}
