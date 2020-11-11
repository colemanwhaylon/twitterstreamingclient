﻿using Akka.Actor;
using Akka.DI.AutoFac;
using Akka.DI.Core;
using Akka.Event;
using Akka.Routing;
using Akka.Util.Internal;
using Autofac;
using AutoMapper;
using JackIsBack.NetCoreLibrary.Actors.Analyzers;
using JackIsBack.NetCoreLibrary.Interfaces;
using JackIsBack.NetCoreLibrary.Messages;
using Serilog;
using System;
using JackIsBack.NetCoreLibrary.Actors.Statistics;
using JackIsBack.NetCoreLibrary.DTO;
using JackIsBack.NetCoreLibrary.Utility;
using Tweetinvi;
using Tweetinvi.Core.DTO;
using Tweetinvi.Events;
using Tweetinvi.Models;
using Tweetinvi.Models.DTO;
using Tweetinvi.Streaming;

namespace JackIsBack.NetCoreLibrary.Actors
{
    public class TweetGeneratorActor : ReceiveActor, ITweetGenerator
    {
        private ILoggingAdapter _logger;
        private static ActorSystem ActorSystem;
        private ISampleStream _sampleStream;
        private static IContainer _container;
        private static IDependencyResolver _resolver;
        private static IActorRef _mainActorRef;
        private static IActorRef _tweetStatisticsActorRef;
        private static IActorRef _totalNumberOfTweetsActorRef;
        private bool _isInitialized = false;
        private static GetAllStatisticsMessageResponse Statistics;

        public TweetGeneratorActor()
        {
            _isInitialized = Initialize();

            // Init MainActor
            //_mainActorRef = ActorSystem.ActorOf(Props.Create<MainActor>().WithRouter(FromConfig.Instance), "MainActor");
            _mainActorRef = Context.System.ActorOf(Props.Create<MainActor>().WithRouter(FromConfig.Instance), "MainActor");
            _tweetStatisticsActorRef = Context.System.ActorOf(Props.Create<TweetStatisticsActor>(), "TweetStatisticsActor");
            _totalNumberOfTweetsActorRef = Context.System.ActorOf(Props.Create<TotalNumberOfTweetsActor>(), "TotalNumberOfTweetsActor"); 

            _tweetStatisticsActorRef.Tell(new TimeKeeperActorMessage(DateTime.Now.Ticks, null));

            Receive<InitToggleCommandRequest>(HandleInitToggleCommandRequest);
            Receive<ChangeTotalNumberOfTweetsMessage>(HandleChangeTotalNumberOfTweetsMessage);
        }

        private void HandleChangeTotalNumberOfTweetsMessage(ChangeTotalNumberOfTweetsMessage message)
        {
            _logger.Debug($"Total Tweet NewTotal = {message.NewTotal}");
        }

        private void HandleInitToggleCommandRequest(InitToggleCommandRequest command)
        {
            if (command == InitToggleCommandRequest.StartUp)
            {
                this.Run();
                Sender.Tell(InitToggleCommandResponse.StartedUp, Self);
            }
            else if (command == InitToggleCommandRequest.Shutdown)
            {
                this.Stop();
                Sender.Tell(InitToggleCommandResponse.ShutdownCompletely, Self);
                this._isInitialized = false;
            }
        }

        private bool Initialize()
        {
            try
            {
                InitializeDIContainer();
            }
            catch (Exception exception)
            {
                _logger.Debug($"Message: {exception.Message}, StackTrace: {exception.StackTrace}");
            }

            return true;
        }


        private void InitializeDIContainer()
        {
            var logger = new LoggerConfiguration()
                //.WriteTo.Seq("http://localhost:5341") //todo:enable Seq before release to prod.
                .WriteTo.ColoredConsole()
                .WriteTo.RollingFile("log.txt",
                    Serilog.Events.LogEventLevel.Verbose,
                    fileSizeLimitBytes: 2048,
                    retainedFileCountLimit: 1,
                    buffered: true)
                .MinimumLevel.Verbose()
                .CreateLogger();
            Serilog.Log.Logger = logger;

            var builder = new ContainerBuilder();

            builder.RegisterType<TwitterInfo>().As<ITwitterInfo>();

            var twitterInfo = new TwitterInfo();
            var twitterClient = new TwitterClient(twitterInfo.Secrets.Key, twitterInfo.Secrets.SecretKey,
                twitterInfo.Secrets.AccessToken, twitterInfo.Secrets.AccessTokenSecret);

            builder.RegisterInstance(twitterClient).As<TwitterClient>();
            builder.RegisterType<TweetGeneratorActor>().As<ITweetGenerator>();

            //Register All Actors
            builder.RegisterType<TotalNumberOfTweetsActor>().InstancePerLifetimeScope();
            builder.RegisterType<TweetAverageActor>().InstancePerLifetimeScope();
            builder.RegisterType<TopEmojisUsedActor>().InstancePerLifetimeScope();
            builder.RegisterType<PercentOfTweetsContainingEmojisActor>().InstancePerLifetimeScope();
            builder.RegisterType<TopHashTagsActor>().InstancePerLifetimeScope();
            builder.RegisterType<PercentOfTweetsWithUrlActor>().InstancePerLifetimeScope();
            builder.RegisterType<PercentOfTweetsWithPhotoUrlActor>().InstancePerLifetimeScope();
            builder.RegisterType<TopDomainsActor>().InstancePerLifetimeScope();
            builder.RegisterType<TweetStatisticsActor>().InstancePerLifetimeScope();
            builder.RegisterType<TimeKeeperActor>().InstancePerLifetimeScope();
            builder.RegisterType<MainActor>();

            //Register Analyzer Actors
            builder.RegisterType<PercentOfTweetsContainingEmojisAnalyzerActor>().InstancePerLifetimeScope();
            builder.RegisterType<PercentOfTweetsWithPhotoUrlAnalyzerActor>().InstancePerLifetimeScope();
            builder.RegisterType<PercentOfTweetsWithUrlAnalyzerActor>().InstancePerLifetimeScope();
            builder.RegisterType<TopDomainsAnalyzerActor>().InstancePerLifetimeScope();
            builder.RegisterType<TopEmojisUsedAnalyzerActor>().InstancePerLifetimeScope();
            builder.RegisterType<TopHashTagsAnalyzerActor>().InstancePerLifetimeScope();
            builder.RegisterType<TweetAverageAnalyzerActor>().InstancePerLifetimeScope();

            //Register Messages
            builder.RegisterType<ChangeTotalNumberOfTweetsMessage>();
            builder.RegisterType<TimeKeeperActorMessage>();

            //AutoMapper Mapping types:
            builder.RegisterType<MyTweetDTO>().As<IMyTweetDTO>();
            builder.RegisterType<TweetDTO>();

            var config = new MapperConfiguration(cfg =>
            {
                cfg.CreateMap<IUser, IUserDTO>();
                cfg.CreateMap<ITweet, TweetDTO>();
            });
            var mapper = config.CreateMapper();
            builder.RegisterInstance<IMapper>(mapper);

            _container = builder.Build();
            _resolver = new AutoFacDependencyResolver(_container, Context.System);
            ActorSystem = Context.System;
            _logger = Context.GetLogger();
        }

        public void Run()
        {
            _logger.Debug("Run was called.");

            var twitterClient = _container.Resolve(typeof(TwitterClient)).AsInstanceOf<TwitterClient>();
            _sampleStream = twitterClient.Streams.CreateSampleStream();
            _sampleStream.TweetReceived += SampleStreamOnTweetReceived;
            _sampleStream.StartAsync();

            _logger.Debug("Run Finished.");
        }

        private void SampleStreamOnTweetReceived(object? sender, TweetReceivedEventArgs e)
        {
            //Making the executive decision not to process & count tweets with no text command.
            if (!string.IsNullOrEmpty(e.Tweet.Text))
            {
                //Increment Tweet Count
                var changeTotalNumberOfTweetsMessage = new ChangeTotalNumberOfTweetsMessage(Operation.Increase, 1, false);

                _totalNumberOfTweetsActorRef.Tell(changeTotalNumberOfTweetsMessage);

                // Instantiate instance of IMyTweetDTO and pass to MainActor
                var myTweetDto = _container.Resolve<IMyTweetDTO>().AsInstanceOf<MyTweetDTO>();
                myTweetDto.CreatedBy = e.Tweet.CreatedBy.ScreenName ?? string.Empty;
                myTweetDto.CreatedById = e.Tweet.CreatedBy.Id;
                myTweetDto.TweetId = e.Tweet.Id;
                myTweetDto.Tweet = (e.Tweet.Text.Length > 140) ?
                    e.Tweet.Text.Substring(0, 140) :
                    e.Tweet.Text;

                _mainActorRef.Tell(myTweetDto);
            }
        }

        public void Stop()
        {
            _logger.Debug("Stop was called.");

            _sampleStream.Stop();
            _sampleStream.TweetReceived -= null;

            _logger.Debug("Stop Finished.");

            _tweetStatisticsActorRef.Tell(new TimeKeeperActorMessage(null, DateTime.Now.Ticks));
        }
    }
}