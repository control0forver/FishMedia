﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FishMedia.Servers.HTTP
{
    public interface ILogger
    {
        void Log(object message);
    }
}
