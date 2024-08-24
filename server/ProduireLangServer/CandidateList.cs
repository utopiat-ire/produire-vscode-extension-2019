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
		readonly List<CompletionItem> candidateList = new List<CompletionItem>();
		readonly Dictionary<string, string> description = new Dictionary<string, string>();
		ProduireFile rdr;

		static readonly YomiganaTable yomiTable = new YomiganaTable();
		static readonly List<CompletionItem> snippetCandidateList = new List<CompletionItem>();

		static AssistCandidateList()
		{
			CreateSnippet();
		}
		public AssistCandidateList(ProduireFile rdr)
		{
			this.rdr = rdr;
		}

		public CompletionItem[] ToArray()
		{
			return candidateList.ToArray();
		}

		internal void Clear()
		{
			candidateList.Clear();
		}

		internal void EnumerateCandidates()
		{
			candidateList.Clear();
			candidateList.AddRange(snippetCandidateList);

			List<string> particles = new List<string>();
			foreach (var verb in rdr.References.VerbList.ToArray())
			{
				if (!verb.IsVisible(VisibilityTarget.CodeAssist)) continue;

				//動詞
				string verbName = verb.Name;
				Add(verbName, CompletionItemKind.Method, null, verb.Yomigana);

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
				Add(particle, CompletionItemKind.Operator, "助詞", null);
			}

			foreach (IPNamespace ns in rdr.References)
			{
				EnumerateCandidateList(ns, candidateList);
			}
		}
		internal void EnumerateCandidateList(IPNamespace ns, List<CompletionItem> candidates)
		{
			foreach (PType pType in ns.Types.GetList())
			{
				if (!pType.IsVisible(VisibilityTarget.CodeAssist)) continue;
				if (!pType.IsComplate) continue;
				if (pType.IsEnum) continue;

				if (pType.IsGlobalClass)
				{
					if (pType.Properties == null) continue;

					foreach (var pair in pType.Properties)
					{
						if (!pair.Value.IsVisible(VisibilityTarget.CodeAssist)) continue;
						var prop = pair.Value;
						Add(prop.Name, CompletionItemKind.Property, prop.GetDescription(), null);
					}
				}
				else if (pType.IsVisible(VisibilityTarget.CodeAssist))
				{
					Add(pType.Name, CompletionItemKind.Property, pType.GetDescription(), null);
				}
			}
		}
		private void Add(string name, CompletionItemKind kind, string doc, string yomi)
		{
			candidateList.Add(new CompletionItem
			{
				filterText = name,
				label = name,
				documentation = doc,
				kind = kind,
				insertText = name
			});
			if (yomi == null) yomi = GetYomigana(name);
			if (name != yomi)
			{
				candidateList.Add(new CompletionItem
				{
					filterText = yomi,
					label = name,
					documentation = doc,
					kind = kind,
					insertText = name
				});
			}
			if (yomi == null) yomi = name;
			if (yomi != null)
			{
				string roman = NamingComparer.ToRoman(yomi);
				if (!string.IsNullOrEmpty(roman))
				{
					candidateList.Add(new CompletionItem
					{
						filterText = roman,
						label = name,
						documentation = doc,
						kind = kind,
						insertText = name
					});
				}
			}
		}

		internal static string GetYomigana(string text)
		{
			if (string.IsNullOrEmpty(text)) return null;
			return yomiTable.GetYomigana(text);
		}

		private static void CreateSnippet()
		{
			AddSnippet("繰り返す", "くりかえす", "繰り返す\r\n$1\r\nそして", null);
			AddSnippet("繰り返しから抜け出す", "くりかえしから抜け出す", "繰り返しから抜け出す", null);
			AddSnippet("回数繰り返す", "かいくりかえす", "${1:《回数》}回、繰り返す\r\n$3\r\nそして", null);
			AddSnippet("カウントして繰り返す", "かうんとしてくりかえす", "${1:《カウント変数》}を${2:《開始》}から${3:《終了》}まで増やしながら繰り返す\r\n$4\r\nそして", null);
			AddSnippet("それぞれ繰り返す", "それぞれくりかえす", "${1:《配列》}のすべての${2:《要素》}についてそれぞれ繰り返す\r\n$3\r\nそして", null);
			AddSnippet("もし文", "もしぶん", "${1:《条件式》}なら\r\n$2\r\nそして", null);
			AddSnippet("もし一文", "もしいちぶん", "${1:《条件式》}なら、", null);
			AddSnippet("分岐", "ぶんき", "${1:《値》}について分岐\r\n$2\r\nそして", null);
			AddSnippet("分岐終わり", "ぶんきおわり", "分岐終わり", null);
			AddSnippet("例外監視", "れいがいかんし", "例外監視\r\n$1\r\n発生した場合\r\n$2\r\nそして", null);

			AddSnippet("手順", "てじゅん", "$1手順\r\n$2\r\n終わり", null);
			AddSnippet("を設定する手順", "をせっていするてじゅん", "を設定する手順\r\n$1\r\n終わり", null);
			AddSnippet("を取得する手順", "をしゅとくするてじゅん", "を取得する手順\r\n$1\r\n終わり", null);
			AddSnippet("はじめの手順", "はじめのてじゅん", "はじめの手順\r\n$1\r\n終わり", null);
			AddSnippet("種類", "しゅるい", "${1:《種類名》}とは\r\n$2\r\n終わり", null);

			AddSnippet("かつ条件", "かつじょうけん", "${1:《式》}かつ${2:《式》}", null);
			AddSnippet("または条件", "またはじょうけん", "${1:《式》}または${2:《式》}", null);

			AddSnippet("とは", null, "とは\r\n$1\r\n終わり", null);
			AddSnippet("でない", null, "でない", null);
			AddSnippet("より大きい", "よりおおきい", "より大きい", null);
			AddSnippet("より小さい", "よりちいさい", "より小さい", null);
			AddSnippet("未満", "みまん", "より小さい", null);
			AddSnippet("そうでなければ", null, "そうでなければ", null);
			AddSnippet("他なら", "ほかなら", "他なら", null);
			AddSnippet("他でもし", "ほかでもし", "他でもし${1:《条件式》}なら", null);
			AddSnippet("そして", null, "そして", null);
			AddSnippet("もし終わり", "もしおわり", "もし終わり", null);
			AddSnippet("繰り返し終わり", "くりかえしおわり", "繰り返し終わり", null);
			AddSnippet("する", null, "する", null);
			AddSnippet("したもの", null, "したもの", null);
			AddSnippet("は、", null, "は、", null);
			AddSnippet("代入", "だいにゅう", "${1:《変数》}は、${2:《式》}\r\n", null);
			AddSnippet("を～とする", "をとする", "${1:《式》}を${2:《変数》}とする\r\n", null);
			AddSnippet("自分", "じぶん", null, null);
			AddSnippet("無", "む", "無", null);
			AddSnippet("これ", null, null, null);
			AddSnippet("それ", null, null, null);
			AddSnippet("の場合", "のばあい", "の場合", null);

			AddSnippet("※" + KomeWords.コンソール, "こめこんそーる", null, null);
			AddSnippet("※" + KomeWords.プラグインなし, "こめぷらぐいんなし", null, null);
			AddSnippet("※" + KomeWords.プラグイン明示, "こめぷらぐいんめいじ", null, null);
			AddSnippet("※" + KomeWords.ウェブアプリ, "こめうぇぶあぷり", null, null);
			AddSnippet("※" + KomeWords.宣言必須, "こめせんげんひっす", null, null);
			AddSnippet("※" + KomeWords.管理者実行, "こめかんりしゃじっこう", null, null);
			AddSnippet("※" + KomeWords.よみがな, "こめよみがな", null, null);
			AddSnippet("※説明", "こめせつめい", null, null);
			AddSnippet("※戻り値", "こめもどりち", null, null);

			AddSnippet("抽象的", "ちゅうしょうてき", null, null);
			AddSnippet("実装なし", "じっそうなし", null, null);
			AddSnippet("単一種類", "たんいつしゅるい", null, null);
		}
		private static void AddSnippet(string name, string yomi, string snippetText, string doc)
		{
			if (snippetText == null) snippetText = name;
			snippetCandidateList.Add(new CompletionItem
			{
				filterText = name,
				label = name,
				documentation = doc,
				kind = CompletionItemKind.Snippet,
				insertText = snippetText,
				insertTextFormat = InsertTextFormat.Snippet
			});
			snippetCandidateList.Add(new CompletionItem
			{
				filterText = yomi,
				label = name,
				documentation = doc,
				kind = CompletionItemKind.Snippet,
				insertText = snippetText,
				insertTextFormat = InsertTextFormat.Snippet
			});
			if (yomi == null) yomi = name;
			string roma = NamingComparer.ToRoman(yomi);
			snippetCandidateList.Add(new CompletionItem
			{
				filterText = roma,
				label = name,
				documentation = doc,
				kind = CompletionItemKind.Snippet,
				insertText = snippetText,
				insertTextFormat = InsertTextFormat.Snippet
			});
		}
	}

}
