﻿//
// Copyright 2012 Microsoft Corporation
// 
// Licensed under the Apache License, Version 2.0 (the "License");
//  you may not use this file except in compliance with the License.
//  You may obtain a copy of the License at
//    http://www.apache.org/licenses/LICENSE-2.0
// 
//  Unless required by applicable law or agreed to in writing, software
//  distributed under the License is distributed on an "AS IS" BASIS,
//  WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
//  See the License for the specific language governing permissions and
//  limitations under the License.
//

using System;
using System.Collections.Generic;
using Microsoft.Phone.Maps.Services;

namespace WindowsAzure.Acs.Oauth2.Client.WinRT
{
    /// <summary>
    /// Helper class for processing HTTP query strings.
    /// </summary>
    internal class HttpQueryStringParser
    {
        public static Dictionary<string, string> Parse(string queryString)
        {
            var parser = new HttpQueryStringParser(queryString);
            return parser._values;
        } 

        private Dictionary<string, string> _values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="queryString">Query string.</param>
        internal HttpQueryStringParser(string queryString)
        {
            if (queryString[0] == '?') queryString = queryString.Remove(0, 1);

            string[] pairs = queryString.Split('&');

            foreach (string pair in pairs)
            {
                if (string.IsNullOrEmpty(pair)) continue;

                int pos = pair.IndexOf('=');
                string name = pair.Substring(0, pos);
                string value = pair.Substring(pos + 1);
                _values.Add(name, value);
            }
        }

        /// <summary>
        /// Gets parameter by name.
        /// </summary>
        /// <param name="parameterName">Parameter name.</param>
        /// <returns>Parameter value.</returns>
        internal string this[string parameterName]
        {
            get
            {
                return _values[parameterName];
            }
        }
    }
}