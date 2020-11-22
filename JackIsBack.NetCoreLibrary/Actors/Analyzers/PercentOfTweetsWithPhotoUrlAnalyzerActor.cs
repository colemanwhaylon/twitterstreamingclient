﻿using System.Text.RegularExpressions;
using Akka.Actor;
using Akka.Event;
using JackIsBack.NetCoreLibrary.DTO;
using JackIsBack.NetCoreLibrary.Interfaces;
using JackIsBack.NetCoreLibrary.Utility;

namespace JackIsBack.NetCoreLibrary.Actors.Analyzers
{
    public class PercentOfTweetsWithPhotoUrlAnalyzerActor : ReceiveActor
    {
        private readonly ILoggingAdapter _logger = Context.GetLogger();
        private int _tweetCountWithPhotoUrl = 0;
        private double? _previousPercentOfTweetsWithPhotoUrl = 0;

        public PercentOfTweetsWithPhotoUrlAnalyzerActor()
        {
            Receive<MyTweetDTO>(AnalyzeTwitterMessage);
        }

        private void AnalyzeTwitterMessage(MyTweetDTO message)
        {
            var regex = @"(http|ftp|https):\/\/[\w\-_]+(\.[\w\-_]+)+([\w\-\.,@?^=%&amp;:/~\+#]*[\w\-\@?^=%&amp;/~\+#])?";
            var result = Regex.Match(message.Tweet, regex);

            if (result.Success)
            {
                ++_tweetCountWithPhotoUrl;

                message.PercentOfTweetsWithPhotoUrl = (((double)_tweetCountWithPhotoUrl) / message.CurrentTweetCount) * 100.0;
                _previousPercentOfTweetsWithPhotoUrl = message.PercentOfTweetsWithPhotoUrl;
            }

            if (message.PercentOfTweetsWithPhotoUrl == 0)
                message.PercentOfTweetsWithPhotoUrl = _previousPercentOfTweetsWithPhotoUrl;

            TweetStatisticsActor.IActorRefs["PercentOfTweetsWithPhotoUrlActor"].Tell(message, Self);
        }
    }
}