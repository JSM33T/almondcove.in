﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Almondcove.Entities.Dedicated
{
    public class Message
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string Mail { get; set; }
        public string MessageText { get; set; }
        public string Topic { get; set; } = "general";
        public string Origin { get; set; }
        public string UserAgent { get; set; }
        public DateTime DateAdded { get; set; } = DateTime.Now;
    }
}
