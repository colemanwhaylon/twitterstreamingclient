﻿using System.Threading.Tasks;
using Akka.Actor;
using JackIsBack.Common.DTO;
using Tweetinvi.Models;

namespace JackIsBack.Common.Actors
{
    public class PercentOfTweetsWithPhotoUrlActor : ReceiveActor
    {
        private static long Count { get; set; } = 0;
        public PercentOfTweetsWithPhotoUrlActor()
        {
            System.Console.WriteLine("PercentOfTweetsWithPhotoUrlActor created.");
            Receive<MyTweetDTO>(HandleTwitterMessageAsync);
        }

        private async void HandleTwitterMessageAsync(MyTweetDTO tweet)
        {
            await Task.Factory.StartNew(() =>
            {
                //var command = new ChangeTweetQuantityCommand(operation: Operation.Increase, 1);
                //var commandManager = new CommandManager();
                //commandManager.Invoke(command);

                System.Console.WriteLine($"PercentOfTweetsWithPhotoUrlActor wrote " + tweet);
            });
        }
    }
}
