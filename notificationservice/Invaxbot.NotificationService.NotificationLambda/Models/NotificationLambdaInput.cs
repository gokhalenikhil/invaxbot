using System;
using System.Collections.Generic;
using System.Text;

namespace Invaxbot.NotificationService.NotificationLambda.Models
{
    public class NotificationLambdaInput
    {
        public string short_pin { get; set; }

        public string s3url { get; set; }

        public string bucket { get; set; }
    }
}
