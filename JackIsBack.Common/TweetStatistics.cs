﻿using System;
using System.Collections.Generic;

namespace JackIsBack.Common.Commands
{
    public static class TweetStatistics
    {
        public static TimeSpan StartDateTime { get; set; }
        public static TimeSpan EndDateTime { get; set; }
        public static long TotalTweetCount = 0;
        public static long AverageTweetsPerHour { get; set; } = 0;
        public static long AverageTweetsPerMinute { get; set; } = 0;
        public static long AverageTweetsPerSecond { get; set; } = 0;
    }

    //docker run --name twitter1 --hostname twitter1 -p 4053:4053 -p 9110:9110 --env ACTORSYSTEM=TwitterStatisticsActorSystem --env CLUSTER_IP=twitter1 --env CLUSTER_PORT=4053 --env CLUSTER_SEEDS="akka.tcp://TwitterStatisticsActorSystem@twitter1:4053" petabridge/lighthouse:latest


    /// <summary>
    /// Updated TweetInstancePerHour to reflect a structure that
    /// can be used to track tweet count over a 24 hour period.
    /// </summary>

    public struct TweetInstancePerHour
    {
        public int Hour { get; set; }
        public int Minute { get; set; }
        public int Second { get; set; }
        public int MilliSecond { get; set; }
        public long TotalTweetCountPerHour { get; set; }
        public long AverageTweetsPerHour { get; set; }
        public long AverageTweetsPerMinute { get; set; } 
        public long AverageTweetsPerSecond { get; set; } 
    }
}