﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace ESLifyEverything.Properties.DataFileTypes
{
    public class Separator
    {
        [JsonInclude]
        public string FormKeySeparator = String.Empty;
        [JsonInclude]
        public bool IDIsSecond = false;
        [JsonInclude]
        public bool ModNameAsString = false;
        [JsonInclude]
        public char ModNameStringCharater = '\"';
    }
}
