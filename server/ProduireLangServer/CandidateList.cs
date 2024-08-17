// Copyright(C) 2019-2024 utopiat.net https://github.com/utopiat-ire/
using LanguageServer.Parameters.TextDocument;
using Produire;
using Produire.Model;
using Produire.TypeModel;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace ProduireLangServer
{

	/// <summary>
	/// 初期状態の入力補完
	/// </summary>
	internal class AssistState
	{
		Dictionary<ProduireFile, AssistCandidateList> condidateLists = new Dictionary<ProduireFile, AssistCandidateList>();

		internal AssistState()
		{
		}

		internal AssistCandidateList GetCompletion(ProduireFile rdr)
		{
			if (!condidateLists.TryGetValue(rdr, out AssistCandidateList list))
			{
				list = new AssistCandidateList(rdr);
				list.EnumerateCandidates();
				condidateLists[rdr] = list;
			}
			return list;
		}

		internal object GetDescription(object data)
		{
			throw new NotImplementedException();
		}
	}

	internal class AssistCandidateList
	{
		List<CompletionItem> candidates = new List<CompletionItem>();
		Dictionary<string, string> description = new Dictionary<string, string>();
		ProduireFile rdr;

		static AssistCandidateList()
		{
		}
		public AssistCandidateList(ProduireFile rdr)
		{
			this.rdr = rdr;
		}

		public CompletionItem[] ToArray()
		{
			return candidates.ToArray();
		}

		internal void Clear()
		{
			candidates.Clear();
		}

		internal void EnumerateCandidates()
		{
			candidates.Clear();

			List<string> particles = new List<string>();
			foreach (var verb in rdr.References.VerbList.ToArray())
			{
				if (!verb.IsVisible(VisibilityTarget.CodeAssist)) continue;

				//動詞
				string verbName = verb.Name;
				string yomi = verb.Yomigana;
				string roman = string.IsNullOrEmpty(yomi) ? null : NamingComparer.FromRoman(yomi);
				Add(verbName, CompletionItemKind.Method, verbName, null, yomi, roman);

				//助詞
				foreach (var method in verb.Overloads)
				{
					var complements = method.Complements;
					if (complements == null) continue;

					for (int i = 0; i < complements.Length; i++)
					{
						var complement = complements[i];
						if (complement is 実補語定義
							&& !(complement is IAttributiveComplement))
						{
							var info = complement as 実補語定義;
							info.Particle.AddNamesTo(particles);
						}
					}
				}
			}
			for (int i = 0; i < particles.Count; i++)
			{
				var particle = particles[i];
				string yomi = GetYomigana(particle);
				string roman = string.IsNullOrEmpty(yomi) ? null : NamingComparer.ToRoman(yomi);
				Add(particle, CompletionItemKind.Operator, particle, null, yomi, roman);
			}

			foreach (IPNamespace ns in rdr.References)
			{
				EnumerateCandidateList(ns, candidates);
			}
		}
		internal void EnumerateCandidateList(IPNamespace ns, List<CompletionItem> candidates)
		{
			foreach (PType pType in ns.Types.GetList())
			{
				if (!pType.IsVisible(VisibilityTarget.CodeAssist)) continue;
				if (!pType.IsComplate) continue;

				if (pType.IsGlobalClass)
				{
					if (pType.Properties == null) continue;

					foreach (var pair in pType.Properties)
					{
						if (!pair.Value.IsVisible(VisibilityTarget.CodeAssist)) continue;
						var prop = pair.Value;
						string yomi = GetYomigana(prop.Name);
						string roman = string.IsNullOrEmpty(yomi) ? null : NamingComparer.FromRoman(yomi);
						Add(prop.Name, CompletionItemKind.Property, prop.GetDescription(), null, yomi, roman);
					}
				}
				else if (pType.IsVisible(VisibilityTarget.CodeAssist))
				{
					string yomi = GetYomigana(pType.Name);
					string roman = string.IsNullOrEmpty(yomi) ? null : NamingComparer.FromRoman(yomi);
					Add(pType.Name, CompletionItemKind.Property, pType.GetDescription(), pType.GetDescription(), yomi, roman);
				}
			}
		}
		private void Add(string name, CompletionItemKind kind, string doc, params string[] texts)
		{
			foreach (string text in texts)
			{
				if (string.IsNullOrEmpty(text)) continue;
				candidates.Add(new CompletionItem
				{
					filterText = text,
					label = name,
					documentation = doc,
					textEdit = new LanguageServer.Parameters.TextEdit { newText = name },
					kind = kind,
					data = name
				});
			}

		}

		static YomiganaTable yomiTable = new YomiganaTable();
		internal static string GetYomigana(string text)
		{
			if (string.IsNullOrEmpty(text)) return null;
			return yomiTable.GetYomigana(text);
		}

	}

}
