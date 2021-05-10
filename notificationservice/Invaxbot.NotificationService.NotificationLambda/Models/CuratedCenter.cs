using System;
using System.Collections.Generic;
using System.Text;

namespace Invaxbot.NotificationService.NotificationLambda.Models
{
    public class CuratedCenter 
    {
        public long center_id { get; set; }
        public string name { get; set; }
        public string pincode { get; set; }
        public bool eighteenplusAvailability { get; set; }
        public bool fortyfiveplusAvailability { get; set; }
        public double weeklyCapacity { get; set; }

        public override string ToString()
        {
            return $"{name}, {pincode}, {weeklyCapacity} slots available\n";
        }
    }
}
