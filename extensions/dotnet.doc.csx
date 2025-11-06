/*---------------------------------------------------------------------------------------------------------

	Kombine XML to Markdown example

	(C) Kollective Networks 2022

	Taken from this gist: https://gist.github.com/lontivero/593fc51f1208555112e0

	This is just temporal. It should be replace for a proper documentation generator.

---------------------------------------------------------------------------------------------------------*/

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;

public static class XmlToMarkdown {

	public static void Convert(string InputFile,string OutputFile){
		string xml = Files.ReadTextFile(InputFile);
		if (xml == string.Empty)
			return;
		XDocument doc = XDocument.Parse(xml);
		if (doc.Root == null){
			Msg.Print("Error parsing XML file: "+InputFile);
			return;
		}
		string md = ToMarkDown(doc.Root);
		Files.WriteTextFile(OutputFile,md);
	}

	internal static string ToMarkDown(XNode e) {
		var templates = new Dictionary<string, string> {
				{"doc", "## {0} ##\n{1}\n"},
				{"type", "# {0}\n{1}\n"},
				{"field", "##### {0}\n{1}\n"},
				{"property", "##### {0}\n{1}\n"},
				{"method", "##### {0}\n{1}\n"},
				{"event", "##### {0}\n{1}\n"},
				{"typeparam", "##### Type Parameter: {0}\n{1}\n"},
				{"summary", "{0}\n"},
				{"remarks", "\n>{0}\n"},
				{"example", "_C# code_\n\n```c#\n{0}\n```\n\n"},
				{"seePage", "[[{1}|{0}]]"},
				{"seeAnchor", "[{1}]({0})"},
				{"param", "- {0}: {1}<br>\n" },
				{"exception", "[[{0}|{0}]]: {1}\n\n" },
				{"returns", "Returns: {0}\n\n"},
				{"none", ""},
				{"c", "{0}"}
			};
		var d = new Func<string?, XElement?, string[]>((att, node) => {
				List<string> list = new List<string>();
				if (att == null)
					return list.ToArray();
				if (node == null)
					return list.ToArray();
				if (node.Attribute(att) != null) {
					XAttribute? xatt = node.Attribute(att);
					if (xatt != null) {
						if (xatt.Value != null)
							list.Add(xatt.Value);
					}
				} else{
					// TODO: This is bugged. It does not add the attribute name in all cases
					list.Add("DefAttribute");
				}
				list.Add(ToMarkDown(node.Nodes()));
				return list.ToArray();
			});
		var methods = new Dictionary<string, Func<XElement, IEnumerable<string>>>
			{
				{"doc", x=>{
					List<string> list = new List<string>();
					if (x == null)
						return list.ToArray();
					XElement? assembly = x.Element("assembly");
					if (assembly == null)
						return list.ToArray();
					XElement? name = assembly.Element("name");
					if (name == null)
						return list.ToArray();
					list.Add(name.Value);
					XElement? members = x.Element("members");
					if (members == null)
						return list.ToArray();
					list.Add(ToMarkDown(members.Elements("member")));
					return list.ToArray();
				}},
				{"type", x=>d("name", x)},
				{"typeparam", x=> d("name", x)},
				{"field", x=> d("name", x)},
				{"property", x=> d("name", x)},
				{"method",x=>d("name", x)},
				{"event", x=>d("name", x)},
				{"summary", x=> new[]{ToMarkDown(x.Nodes()) }},
				{"remarks", x => new[]{ToMarkDown(x.Nodes())}},
				{"example", x => new[]{ToCodeBlock(x.Value)}},
				{"seePage", x=> d("cref", x) },
				{"seeAnchor", x=> { var xx = d("cref", x); xx[0] = xx[0].ToLower(); return xx; }},
				{"param", x => d("name", x) },
				{"exception", x => d("cref", x) },
				{"returns", x => new[]{ToMarkDown(x.Nodes())}},
				{"none", x => new string[0]},
				{"c", x => d("name", x) }
			};
		string name;
		if(e.NodeType== XmlNodeType.Element) {
			XElement el = (XElement) e;
			name = el.Name.LocalName;
			if (name == "member") {
				switch (el.Attribute("name")?.Value[0]) {
					case 'F': name = "field";  break;
					case 'P': name = "property"; break;
					case 'T': name = "type"; break;
					case 'E': name = "event"; break;
					case 'M': name = "method"; break;
					default:  name = "none";   break;
				}
			}
			if (name == "see") {
				bool anchor = false;
				XAttribute? att = el.Attribute("cref");
				if (att != null)
					if (att.Value != null)
						anchor = att.Value.StartsWith("!:#");
				name = anchor ? "seeAnchor" : "seePage";
			}
			try{
				string[] vals = methods[name](el).ToArray();
				string str="";
				switch (vals.Length) {
					case 1: str= string.Format(templates[name], vals[0]);break;
					case 2: str= string.Format(templates[name], vals[0],vals[1]);break;
					case 3: str= string.Format(templates[name], vals[0],vals[1],vals[2]);break;
					case 4: str= string.Format(templates[name], vals[0], vals[1], vals[2], vals[3]);break;
				}
				return str;
			} catch (Exception ex) {
				Msg.Print("Error parsing XML node: "+ex.Message);
				return "";
			}
		}
		if(e.NodeType==XmlNodeType.Text)
			return Regex.Replace( ((XText)e).Value.Replace('\n', ' '), @"\s+", " ");

		return "";
	}

	internal static string ToMarkDown(IEnumerable<XNode> es) {
		return es.Aggregate("", (current, x) => current + ToMarkDown(x));
	}

	static string ToCodeBlock(string s) {
		var lines = s.Split(new char[] {'\n'}, StringSplitOptions.RemoveEmptyEntries);
		var blank = lines[0].TakeWhile(x => x == ' ').Count() - 4;
		return string.Join("\n",lines.Select(x => new string(x.SkipWhile((y, i) => i < blank).ToArray())));
	}
}
