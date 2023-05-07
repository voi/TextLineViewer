using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Windows.Forms;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Reflection;
using System.Linq;
using System.Xml;
using System.Collections;
using System.Collections.Generic;

namespace Chalom
{
	public class ChalomApi
	{
		//
		[STAThread]
		static void Main()
		{
			var filePath = "changelog.md";
			var command = "-a";
			var text = String.Empty;

			var args = new Queue<string>(Environment.GetCommandLineArgs());

			while(args.Count > 0)
			{
				var arg = args.Dequeue();

				switch(arg)
				{
					case "-f":
						if(args.Count > 0)
						{
							filePath = args.Dequeue();
						}
						break;

					case "-a":
					case "-c":
						command = arg;
						break;

					default:
						text = arg;
						break;
				}
			}

			//
			Chalom chalom = new Chalom();

			chalom.Parse(filePath);

			if(command == "-a")
			{
				chalom.AddItem(text);
				chalom.Save(filePath);
			}
			else if(command == "-c")
			{
				chalom.SumTime(System.Console.Out);
			}
		}

		class Chalom
		{
			/// format
			/// 
			/// entry: "^# (\d{4}-\d{2}-\d{2})(?:\s.*)?"
			///
			/// item:  "^*\s\*\(\d{2}:\d{2}\)\* .*"
			private static Regex entryPattern_ = new Regex(@"^# (?<date>\d{4}-\d{2}-\d{2}).*$", RegexOptions.Compiled);
			private static Regex itemPattern_ = new Regex(@"^*\s\*\((?<time>\d{2}:\d{2})\)\*(?<text>.*)$", RegexOptions.Compiled);

			private static string entryFormat_ = "# {0} ############################################################";
			private static string itemFormat_ = "*\t*({0})* {1}";

			class ChalomBlock
			{
				public IEnumerable<string> Body { get { return this.body_; } }

				private List<string> body_ = new List<string>();

				public ChalomBlock()
				{}

				public void AddLine(string line)
				{
					var lastLine = this.body_.LastOrDefault();

					if(String.IsNullOrEmpty(line) && String.IsNullOrEmpty(lastLine))
					{
						return ;
					}

					this.body_.Add(line);
				}

				public void Write(TextWriter writer)
				{
					if(this.body_.Count > 0)
					{
						foreach(var line in this.body_)
						{
							writer.WriteLine(line);
						}

						if((this.body_.Count > 1) && !String.IsNullOrEmpty(this.body_.Last()))
						{
							writer.WriteLine();
						}
					}
				}

				public KeyValuePair<string, DateTime> GetTime(string date)
				{
					DateTime itemTime;

					//
					var matched = Chalom.itemPattern_.Match(this.body_.FirstOrDefault());

					if(!matched.Success)
					{
						return new KeyValuePair<string, DateTime>(String.Empty, DateTime.Now);
					}

					//
					var matchedTime = matched.Groups["time"];
					var matchedText = matched.Groups["text"];

					if(!matchedTime.Success || !matchedText.Success)
					{
						return new KeyValuePair<string, DateTime>(String.Empty, DateTime.Now);
					}

					//
					var time = matchedTime.Value;
					var text = matchedText.Value;

					if(!DateTime.TryParse(String.Format("{0}T{1}:00Z", date, time), out itemTime))
					{
						return new KeyValuePair<string, DateTime>(String.Empty, DateTime.Now);
					}

					return new KeyValuePair<string, DateTime>(text.TrimStart(), itemTime);
				}
			}

			class ChalomItem : ChalomBlock
			{
				public string HeadLine { get; private set; }

				public ChalomItem(string line, string headLine)
				{
					this.HeadLine = headLine;
					this.AddLine(line);
				}
			}

			class ChalomEntry
			{
				private string date_;
				private string header_;

				private ChalomBlock body_ = new ChalomBlock();
				private SortedList<string, ChalomItem> items_ = new SortedList<string, ChalomItem>();

				public ChalomEntry(string line, string date)
				{
					this.date_ = date;
					this.header_ = line;
				}

				public void Parse(string line)
				{
					var matched = Chalom.itemPattern_.Match(line);

					if(matched.Success)
					{

						var matchedTime = matched.Groups["time"];
						var matchedText = matched.Groups["text"];

						var time = (matchedTime.Success ? matchedTime.Value : line);
						var head = (matchedText.Success ? matchedText.Value : line);

						if(!this.items_.ContainsKey(time))
						{
							this.items_.Add(time, new ChalomItem(line, head));
						}
						else
						{
							this.items_[time].AddLine(line);
						}
					}
					else
					{
						if(this.items_.Count > 0)
						{
							this.items_.First().Value.AddLine(line);
						}
						else
						{
							this.body_.AddLine(line);
						}
					}
				}

				public void Write(TextWriter writer)
				{
					writer.WriteLine(this.header_);
					writer.WriteLine();

					this.body_.Write(writer);

					if(this.items_.Count > 0)
					{
						foreach(var item in this.items_.Reverse())
						{
							item.Value.Write(writer);
						}

						writer.WriteLine();
					}
				}

				public void SumTime(TextWriter writer)
				{
					writer.WriteLine(this.header_);
					writer.WriteLine();

					//
					if(this.items_.Count > 1)
					{
						var sumTimes = new SortedList<string, TimeSpan>();
						var lastItem = this.items_.Last().Value.GetTime(this.date_);
						var lastTime = lastItem.Value;
						var totalTime = TimeSpan.Zero;

						foreach(var item in this.items_.Reverse().Skip(1))
						{
							var nameAndTime = item.Value.GetTime(this.date_);
							var diff = (lastTime - nameAndTime.Value);

							if(sumTimes.ContainsKey(nameAndTime.Key))
							{
								sumTimes[nameAndTime.Key] += diff;
							}
							else
							{
								sumTimes.Add(nameAndTime.Key, diff);
							}

							totalTime += diff;
							lastTime = nameAndTime.Value;
						}

						foreach(var sum in sumTimes)
						{
							//
							var correctedValue = 0.0;

							if((sum.Value.TotalMinutes < 15) || ((sum.Value.TotalMinutes % 15) > 8))
							{
								correctedValue = 15;
							}

							//
							var hour = (sum.Value.TotalMinutes + correctedValue) / 60;

							hour -= (hour % 0.25);

							writer.WriteLine(
								String.Format("    {1,4:#0.00}h ({2,4:####}m):  {0}",
									sum.Key, hour, sum.Value.TotalMinutes));
						}

						writer.WriteLine("  -----------------------------------------------------");
						writer.WriteLine(
							String.Format("    {0,4:#0.00}h ({1,4:####}m):  [Total]",
								totalTime.TotalMinutes / 60, totalTime.TotalMinutes));
					}

					//
					writer.WriteLine();
				}
			}

			private ChalomBlock body_ = new ChalomBlock();
			private SortedList<string, ChalomEntry> entries_ = new SortedList<string, ChalomEntry>();

			public Chalom()
			{}

			public void Parse(string filePath)
			{
				if(!File.Exists(filePath))
				{
					return ;
				}

				using(var reader = new StreamReader(filePath))
				{
					var lastLine = String.Empty;

					while(reader.Peek() > 0)
					{
						var line = reader.ReadLine();

						if(String.IsNullOrEmpty(line) && String.IsNullOrEmpty(lastLine))
						{
							continue;
						}

						//
						var matched = entryPattern_.Match(line);

						if(matched.Success)
						{
							var matchedDate = matched.Groups["date"];
							var lastDate = (matchedDate.Success ? matchedDate.Value : line);

							if(!this.entries_.ContainsKey(lastDate))
							{
								this.entries_.Add(lastDate, new ChalomEntry(line, lastDate));
							}
						}
						else
						{
							if(this.entries_.Count > 0)
							{
								this.entries_.First().Value.Parse(line);
							}
							else
							{
								this.body_.AddLine(line);
							}
						}

						lastLine = line;
					}
				}
			}

			public void AddItem(string text)
			{
				var date = DateTime.Now.ToString("yyyy-MM-dd");

				if(!this.entries_.ContainsKey(date))
				{
					this.entries_.Add(date, new ChalomEntry(String.Format(Chalom.entryFormat_, date), date));
				}

				this.entries_[date].Parse(String.Format(Chalom.itemFormat_, DateTime.Now.ToString("HH:mm"), text));
			}

			public void Save(string filePath)
			{
				using(var writer = new StreamWriter(filePath, false, Encoding.UTF8))
				{
					this.body_.Write(writer);

					if(this.entries_.Count > 0)
					{
						foreach(var entry in this.entries_.Reverse())
						{
							entry.Value.Write(writer);
						}

						writer.WriteLine();
					}
				}
			}

			public void SumTime(TextWriter writer)
			{
				foreach(var entry in this.entries_.Reverse())
				{
					entry.Value.SumTime(writer);
				}
			}
		}
	}
}
