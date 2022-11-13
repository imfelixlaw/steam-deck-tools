﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Policy;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using static System.Net.Mime.MediaTypeNames;

namespace PerformanceOverlay
{
    internal class Overlays
    {
        public enum Mode
        {
            FPS,
            Minimal,
            Detail,
            All
        }

        public class Entry
        {
            public String? Text { get; set; }
            public IList<Mode> Include { get; set; } = new List<Mode>();
            public IList<Mode> Exclude { get; set; } = new List<Mode>();
            public IList<Entry> Nested { get; set; } = new List<Entry>();
            public String Separator { get; set; } = "";
            public bool IgnoreMissing { get; set; }

            public static readonly Regex attributeRegex = new Regex("{([^}]+)}", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

            public Entry()
            { }

            public Entry(String text)
            {
                this.Text = text;
            }

            public IEnumerable<Match> AllAttributes
            {
                get
                {
                    return attributeRegex.Matches(Text ?? "");
                }
            }

            private String EvaluateText(Sensors sensors)
            {
                String output = Text ?? "";

                foreach (var attribute in AllAttributes)
                {
                    String attributeName = attribute.Groups[1].Value;
                    String? value = sensors.GetValue(attributeName);
                    if (value is null && IgnoreMissing)
                        return "";
                    output = output.Replace(attribute.Value, value ?? "-");
                }

                return output;
            }

            public String? GetValue(Mode mode, Sensors sensors)
            {
                if (Exclude.Count > 0 && Exclude.Contains(mode))
                    return null;
                if (Include.Count > 0 && !Include.Contains(mode))
                    return null;

                String output = EvaluateText(sensors);

                if (Nested.Count > 0)
                {
                    var outputs = Nested.Select(entry => entry.GetValue(mode, sensors)).Where(output => output != null);
                    if (outputs.Count() == 0)
                        return null;

                    output += String.Join(Separator, outputs);
                }

                if (output == String.Empty)
                    return null;

                return output;
            }
        }

        public static readonly String[] Helpers =
        {
            "<C0=008040><C1=0080C0><C2=C08080><C3=FF0000><C4=FFFFFF><C250=FF8000>",
            "<A0=-4><A1=5><A2=-2><S0=-50><S1=50>",
        };

        public static readonly Entry OSD = new Entry
        {
            Nested = {
                new Entry { Text = "<C4><FR><C><A><A1><S1><C4> FPS", Include = { Mode.FPS } },

                new Entry {
                    Nested =
                    {
                        new Entry
                        {
                            Text = "<C1>BATT<C>",
                            Nested =
                            {
                                new Entry("<C4><A0>{BATT_%}<A><A1><S1> %<S><A>"),
                                new Entry("<C4><A0>{BATT_W}<A><A1><S1> W<S><A>")
                            }
                        },
                        new Entry
                        {
                            Text = "<C1>GPU<C>",
                            Nested =
                            {
                                new Entry("<C4><A0>{GPU_%}<A><A1><S1> %<S><A>"),
                                new Entry("<C4><A0>{GPU_W}<A><A1><S1> W<S><A>"),
                                new Entry { Text = "<C4><A0>{GPU_T}<A><A1><S1> C<S><A>", IgnoreMissing = true }
                            }
                        },
                        new Entry
                        {
                            Text = "<C1>CPU<C>",
                            Nested =
                            {
                                new Entry("<C4><A0>{CPU_%}<A><A1><S1> %<S><A>"),
                                new Entry("<C4><A0>{CPU_W}<A><A1><S1> W<S><A>"),
                                new Entry { Text = "<C4><A0>{CPU_T}<A><A1><S1> C<S><A>", IgnoreMissing = true }
                            }
                        },
                        new Entry
                        {
                            Text = "<C1>RAM<C>",
                            Nested =
                            {
                                new Entry("<C4><A0>{MEM_GB}<A><A1><S1> GiB<S><A>")
                            }
                        },
                        new Entry
                        {
                            Text = "<C2><APP><C>",
                            Nested =
                            {
                                new Entry("<A0><C4><FR><C><A><A1><S1><C4> FPS<C><S><A>")
                            }
                        },
                        new Entry
                        {
                            Text = "<C2><OBJ><C>"
                        }
                    },
                    Separator = "<C250>|<C> ",
                    Include = { Mode.Minimal }
                },

                new Entry { Text = "<C0>MEM<C>   <A0>{MEM_MB}<A><A1><S1> MB<S>", Exclude = { Mode.FPS, Mode.Minimal } },
                new Entry { Text = "<C1>CPU<C>   <A0>{CPU_%}<A><A1><S1> %<S><A>", Exclude = { Mode.FPS, Mode.Minimal } },
                new Entry { Text = "<C1>RAM<C>   <A0>{GPU_MB}<A><A1><S1> MB<S><A>", Exclude = { Mode.FPS, Mode.Minimal } },
                new Entry { Text = "<C2><APP><C> <A0><C4><FR><C><A><A1><S1><C4> FPS<C><S><A> <A0><C4><FT><C><A><A1><S1><C4> ms<C><S><A>", Exclude = { Mode.FPS, Mode.Minimal } },
                new Entry {
                    Text = "<C1>BAT<C>  ",
                    Nested = {
                        new Entry("<A0>{BATT_%}<A><A1><S1> %<S><A>"),
                        new Entry("<A0>{BATT_W}<A><A1><S1> W<S><A>")
                    },
                    Exclude = { Mode.FPS, Mode.Minimal }
                },
                new Entry { Text = "<C2><S1>Frametime<S>", Exclude = { Mode.FPS, Mode.Minimal } },
                new Entry { Text = "<OBJ>", Exclude = { Mode.FPS, Mode.Minimal } },
                new Entry { Text = "<S1> <A0><FT><A><A1> ms<A><S><C>", Exclude = { Mode.FPS, Mode.Minimal } }
            },
            Separator = "\r\n"
        };

        public static String GetOSD(Mode mode, Sensors sensors)
        {
            var sb = new StringBuilder();

            sb.AppendJoin("", Helpers);
            sb.Append(OSD.GetValue(mode, sensors) ?? "");

            return sb.ToString();
        }
    }
}
