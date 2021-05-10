using System;
using System.Collections.Generic;
using System.Text;

namespace Invaxbot.NotificationService.NotificationLambda.Models
{
    public class Subscriber
    {
        public string id { get; set; }
        public string pincode { get; set; }
        public bool min_age_18 { get; set; }
        public bool min_age_45 { get; set; }
        public string short_pin { get; set; }
        public bool active { get; set; } = true;
        public long last_alerted { get; set; } = 0;
        public string alert_center_ids { get; set; }
    }
}
