// Copyright(C) 2019-2024 utopiat.net https://github.com/utopiat-ire/
using LanguageServer;
using LanguageServer.Client;
using LanguageServer.Parameters;
using LanguageServer.Parameters.General;
using LanguageServer.Parameters.TextDocument;
using LanguageServer.Parameters.Workspace;
using Produire;
using Produire.Debugger;
using Produire.Designer.DocumentModel;
using Produire.Model;
using Produire.Model.Phrase;
using Produire.Model.Statement;
using Produire.Parser;
using Produire.TypeModel;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace ProduireLangServer
{
	public class App : ServiceConnection
	{
		Uri workerSpaceRoot;
		TextDocumentManager documentList;
		Dictionary<string, ProduireFile> rdrList = new Dictionary<string, ProduireFile>();
		ScriptParser parser = new ScriptParser();
		PluginManager manager = new PluginManager();
		AssistState assist = new AssistState();

		int maxNumberOfProblems = 1000;
		string pluginsPath = null;

		public App(Stream input, Stream output)
			: base(input, output)
		{
			documentList = new TextDocumentManager();
			documentList.Changed += Documents_Changed;
			parser.ScriptError += new ScriptErrorHandler(parser_ScriptError);
		}

		private void Documents_Changed(object sender, TextDocumentChangedEventArgs e)
		{
			ValidateTextDocument(e.Document);
		}

		protected override Result<InitializeResult, ResponseError<InitializeErrorData>> Initialize(InitializeParams args)
		{
			workerSpaceRoot = args.rootUri;
			var result = new InitializeResult
			{
				capabilities = new ServerCapabilities
				{
					textDocumentSync = TextDocumentSyncKind.Full,
					completionProvider = new CompletionOptions
					{
						resolveProvider = true,
						triggerCharacters = new string[] { "」", ")", "）", "、", "。", "]", "］" }
					},
					documentSymbolProvider = true,
					workspaceSymbolProvider = true,
					hoverProvider = true,
					definitionProvider = true,
					typeDefinitionProvider = true,
					//codeLensProvider = new CodeLensOptions { resolveProvider = true },
					documentHighlightProvider = true,
					referencesProvider = true,
					renameProvider = true,
					documentFormattingProvider = true
				}
			};
			return Result<InitializeResult, ResponseError<InitializeErrorData>>.Success(result);
		}

		protected override void DidOpenTextDocument(DidOpenTextDocumentParams args)
		{
			var document = args.textDocument;
			documentList.Add(document);
			errors.Clear();
			ProduireFile rdr = new ProduireFile();
			try
			{
				parser.Parse(document.text, rdr, manager);
				rdr.FullPath = document.uri.LocalPath.Substring(1);
				rdrList.Add(document.uri.LocalPath, rdr);
				//Logger.Instance.Log(document.uri + "を開きました。");
			}
			catch (Exception ex)
			{
				Logger.Instance.Error(ex.Message);
			}
		}

		protected override void DidChangeTextDocument(DidChangeTextDocumentParams args)
		{
			var document = args.textDocument;
			documentList.Change(document.uri, document.version, args.contentChanges);
			if (rdrList.TryGetValue(document.uri.LocalPath, out ProduireFile rdr))
			{
				if (args.contentChanges.Length > 0)
				{
					rdr.FullPath = document.uri.LocalPath.Substring(1);
					try
					{
						parser.Parse(args.contentChanges[0].text, rdr);
					}
					catch (Exception ex)
					{
						Logger.Instance.Error(ex.Message);
					}
				}
			}

			//Logger.Instance.Log(document.uri + "が変更されました。");
		}

		protected override void DidCloseTextDocument(DidCloseTextDocumentParams args)
		{
			var document = args.textDocument;
			documentList.Remove(document.uri);
			rdrList.Remove(document.uri.LocalPath);
			//Logger.Instance.Log(document.uri + "を閉じました。");
		}

		protected override void DidChangeConfiguration(DidChangeConfigurationParams args)
		{
			maxNumberOfProblems = args?.settings?.jplproduire?.maxNumberOfProblems ?? maxNumberOfProblems;
			//Logger.Instance.Log("maxNumberOfProblemsの値:" + maxNumberOfProblems);

			//プラグインフォルダ
			pluginsPath = args?.settings?.jplproduire?.pluginsPath ?? pluginsPath;
			PluginManager.PluginsPath = pluginsPath;
			PluginManager.GetStandards();
			manager = new PluginManager();
			if (Directory.Exists(pluginsPath))
			{
				manager.AddPluginPath(pluginsPath);
				//Logger.Instance.Log("プラグインフォルダ:" + PluginManager.PluginsPath);
			}
			else
			{
				Logger.Instance.Log("プラグインフォルダが見つかりません。" + PluginManager.PluginsPath);
			}

			foreach (var document in documentList.All)
			{
				ValidateTextDocument(document);
			}
		}

		private void ValidateTextDocument(TextDocumentItem document)
		{
			var diagnostics = new List<Diagnostic>();
			if (rdrList.TryGetValue(document.uri.LocalPath, out ProduireFile rdr))
			{
				errors.Clear();
				try
				{
					parser.Parse(document.text, rdr, manager);
				}
				catch (Exception ex)
				{
					Logger.Instance.Error(ex.Message);
				}

				foreach (var error in errors)
				{
					int lineNo = error.Range.LineNo;
					if (lineNo < 0) lineNo = 0;

					var d = new Diagnostic
					{
						severity = DiagnosticSeverity.Error,
						range = GetRange(error.Range.Range),
						message = "[" + error.Code + "]" + error.Description,
						source = "プロデル"
					};
					diagnostics.Add(d);
					if (diagnostics.Count > maxNumberOfProblems) break;
				}
			}

			Proxy.TextDocument.PublishDiagnostics(new PublishDiagnosticsParams
			{
				uri = document.uri,
				diagnostics = diagnostics.ToArray()
			});
		}
		List<ErrorInfoItem> errors = new List<ErrorInfoItem>();
		private bool parser_ScriptError(string message, BreakPoint breakPoint)
		{
			errors.Add(new ErrorInfoItem("", message, breakPoint));
			return false;
		}

		protected override Result<CompletionResult, ResponseError> Completion(CompletionParams args)
		{
			if (rdrList.TryGetValue(args.textDocument.uri.LocalPath, out ProduireFile rdr))
			{
				var list = assist.GetCompletion(rdr);
				return Result<CompletionResult, ResponseError>.Success(new CompletionResult(list.ToArray()));
			}
			else
			{
				return Result<CompletionResult, ResponseError>.Error(new ResponseError { message = "読み込まれていません。" });
			}
		}

		protected override Result<CompletionItem, ResponseError> ResolveCompletionItem(CompletionItem args)
		{
			return Result<CompletionItem, ResponseError>.Success(args);
		}

		protected override Result<DocumentSymbolResult, ResponseError> DocumentSymbols(DocumentSymbolParams args)
		{
			List<SymbolInformation> list = new List<SymbolInformation>();
			if (rdrList.TryGetValue(args.textDocument.uri.LocalPath, out ProduireFile rdr))
			{
				foreach (var pair in rdr.Constructs)
				{
					var construct = pair.Value as Construct;
					if (!(construct is GlobalConstruct))
					{
						SymbolInformation info = new SymbolInformation()
						{
							name = construct.Name,
							deprecated = false,
							containerName = construct.Name,
							kind = SymbolKind.Class,
							location = GetLocation(construct.Range, rdr)
						};
						list.Add(info);
					}
					foreach (var element in construct.CodeList)
					{
						if (element is DeclareFieldStatement)
						{
							var field = element as DeclareFieldStatement;

							SymbolInformation info2 = new SymbolInformation()
							{
								name = field.Variable.Name,
								deprecated = false,
								containerName = field.Variable.Name,
								kind = SymbolKind.Field,
								location = GetLocation(field.Range, rdr)
							};
							list.Add(info2);
						}
						else if (element is Procedure)
						{
							var procedure = element as Procedure;
							if (construct is GlobalConstruct && procedure is FixedProcedure) continue;

							SymbolInformation info2 = new SymbolInformation()
							{
								name = procedure.UniqueName,
								deprecated = false,
								containerName = procedure.UniqueName,
								kind = SymbolKind.Method,
								location = GetLocation(procedure.Range, rdr)
							};
							list.Add(info2);
						}
					}
				}
			}
			DocumentSymbolResult ret = new DocumentSymbolResult(list.ToArray());
			return Result<DocumentSymbolResult, ResponseError>.Success(ret);
		}

		protected override Result<DocumentHighlight[], ResponseError> DocumentHighlight(TextDocumentPositionParams args)
		{
			if (!rdrList.TryGetValue(args.textDocument.uri.LocalPath, out ProduireFile rdr))
			{
				return Result<DocumentHighlight[], ResponseError>.Error(GetError("プログラムが読み込まれていません。"));
			}
			var element = GetElementFromPosition(rdr, args.position);
			List<DocumentHighlight> list = new List<DocumentHighlight>();

			if (element is IPhrase)
			{
				var work = element as IPhrase;
				ReferenceSearcher searcher = new ReferenceSearcher();
				searcher.Search(rdr, work);
				foreach (var found in searcher.Founds)
				{
					var dh = new DocumentHighlight
					{
						range = GetRange(found.Phrase.Range),
						kind = DocumentHighlightKind.Read
					};
					list.Add(dh);
				}
			}

			return Result<DocumentHighlight[], ResponseError>.Success(list.ToArray());
		}
		protected override Result<Hover, ResponseError> Hover(TextDocumentPositionParams args)
		{
			if (!rdrList.TryGetValue(args.textDocument.uri.LocalPath, out ProduireFile rdr))
			{
				return Result<Hover, ResponseError>.Error(GetError("プログラムが読み込まれていません。"));
			}
			string tips = "";
			var hitElement = GetElementFromPosition(rdr, args.position);
			if (hitElement is IPhrase)
			{
				var phrase = hitElement as IPhrase;
				tips = phrase.DisplayText;
			}
			var hover = new Hover()
			{
				contents = new HoverContents(tips)
			};
			return Result<Hover, ResponseError>.Success(hover);
		}
		protected override Result<LocationSingleOrArray, ResponseError> GotoDefinition(TextDocumentPositionParams args)
		{
			if (!rdrList.TryGetValue(args.textDocument.uri.LocalPath, out ProduireFile rdr))
			{
				return Result<LocationSingleOrArray, ResponseError>.Error(GetError("プログラムが読み込まれていません。"));
			}
			var element = GetElementFromPosition(rdr, args.position);

			Location loc = new Location();
			if (element is 種類字句)
			{
				var ptype = (element as 種類字句).GetPType();
				if (ptype is Construct)
				{
					loc = GetLocation((ptype as Construct).Range);
				}
			}
			else if (element is StaticCallExpression)
			{
				var work = element as StaticCallExpression;
				if (work.MethodInfo is Procedure)
				{
					loc = GetLocation((work.MethodInfo as Procedure).Range);
				}
			}
			else if (element is NoArgsProcedureCallStatement)
			{
				var procedure = (element as NoArgsProcedureCallStatement).Procedure;
				loc = GetLocation(procedure.Range);
			}
			else if (element is 動詞字句)
			{
				var expr = (element as 動詞字句).ParentSentence;
				var procedure = (expr != null) ? expr.GetMethodInfo() : null;
				if (procedure is Procedure)
				{
					loc = GetLocation((procedure as Procedure).Range);
				}
			}
			else if (element is 形式補語字句)
			{
				var expr = (element as 形式補語字句).ParentSentence;
				var procedure = (expr != null) ? expr.GetMethodInfo() : null;
				if (procedure is Procedure)
				{
					loc = GetLocation((procedure as Procedure).Range);
				}
			}
			else if (element is IVariableToken)
			{
				var work = element as IVariableToken;
				loc = GetLocation(work.Variable);
			}
			loc.uri = args.textDocument.uri;
			LocationSingleOrArray ret = new LocationSingleOrArray(loc);
			return Result<LocationSingleOrArray, ResponseError>.Success(ret);
		}

		/// <summary>定義へ移動する</summary>
		internal Location GetLocation(PVariable variable)
		{
			if (variable is フィールド変数定義)
			{
				var work2 = variable as フィールド変数定義;
				if (work2.DeclaringType is Construct)
				{
					var construct = work2.DeclaringType as Construct;
					//行移動
					foreach (var item in construct.CodeList)
					{
						if (!(item is DeclareFieldStatement)) continue;

						var work3 = item as DeclareFieldStatement;
						if (work3.Variable == work2)
						{
							return GetLocation(work3.Range);
						}
					}
				}
			}
			return new Location();
		}

		protected override Result<CodeLens[], ResponseError> CodeLens(CodeLensParams args)
		{
			return base.CodeLens(args);
		}
		protected override Result<LocationSingleOrArray, ResponseError> GotoTypeDefinition(TextDocumentPositionParams args)
		{
			return base.GotoTypeDefinition(args);
		}
		protected override Result<Location[], ResponseError> FindReferences(ReferenceParams args)
		{
			if (!rdrList.TryGetValue(args.textDocument.uri.LocalPath, out ProduireFile rdr))
			{
				return Result<Location[], ResponseError>.Error(GetError("プログラムが読み込まれていません。"));
			}
			var element = GetElementFromPosition(rdr, args.position);
			List<Location> list = new List<Location>();

			if (element is IVariableToken)
			{
				var work = element as IVariableToken;
				PVariableSearcher searcher = new PVariableSearcher();
				searcher.Search(rdr, work.Variable);
				foreach (var found in searcher.Founds)
				{
					list.Add(GetLocation(found.Phrase.Range, found.ProduireFile));
				}
			}
			else if (element is IPhrase)
			{
				var work = element as IPhrase;
				ReferenceSearcher searcher = new ReferenceSearcher();
				searcher.Search(rdr, work);
				foreach (var found in searcher.Founds)
				{
					list.Add(GetLocation(found.Phrase.Range, found.ProduireFile));
				}
			}

			return Result<Location[], ResponseError>.Success(list.ToArray());
		}

		protected override Result<WorkspaceEdit, ResponseError> Rename(RenameParams args)
		{
			if (!rdrList.TryGetValue(args.textDocument.uri.LocalPath, out ProduireFile rdr))
			{
				return Result<WorkspaceEdit, ResponseError>.Error(GetError("プログラムが読み込まれていません。"));
			}
			var element = GetElementFromPosition(rdr, args.position);
			List<TextEdit> list = new List<TextEdit>();

			if (element is IPhrase)
			{
				var work = element as IPhrase;
				ReferenceSearcher searcher = new ReferenceSearcher();
				searcher.Search(rdr, work);
				foreach (var found in searcher.Founds)
				{
					list.Add(new TextEdit
					{
						range = GetRange(found.Phrase.Range),
						newText = args.newName
					});
				}
			}

			var edit = new TextDocumentEdit
			{
				edits = list.ToArray(),
				textDocument = new VersionedTextDocumentIdentifier { uri = GetUri(rdr) }
			};
			WorkspaceEdit we = new WorkspaceEdit { documentChanges = new TextDocumentEdit[] { edit } };
			return Result<WorkspaceEdit, ResponseError>.Success(we);
		}

		protected override Result<TextEdit[], ResponseError> DocumentFormatting(DocumentFormattingParams args)
		{
			if (!rdrList.TryGetValue(args.textDocument.uri.LocalPath, out ProduireFile rdr))
			{
				return Result<TextEdit[], ResponseError>.Error(GetError("プログラムが読み込まれていません。"));
			}

			ScriptGenerator generator = new ScriptGenerator();
			CleanupCodeBuilder builder = new CleanupCodeBuilder();
			generator.Generate(rdr, builder);

			List<TextEdit> list = new List<TextEdit>();
			if (rdr.CodeList.Count > 0)
			{
				var start = new CodePosition(1, 1, 1);
				var end = rdr.CodeList[rdr.CodeList.Count - 1].Range.End;
				list.Add(new TextEdit
				{
					range = GetRange(new CodeRange(start, end)),
					newText = builder.ToString()
				});
			}
			return Result<TextEdit[], ResponseError>.Success(list.ToArray());
		}

		private ICodeElement GetElementFromPosition(ProduireFile rdr, Position position)
		{
			var element = rdr.GetElementFromPosition((int)position.line + 1, (int)position.character + 1);
			//Logger.Instance.Log("{" + position.line + "/" + position.character + "}");
			return element;
		}

		private Uri GetUri(ProduireFile produireFile)
		{
			foreach (var pair in rdrList)
			{
				if (pair.Value == produireFile)
				{
					return new Uri("file://" + pair.Key);
				}
			}
			return null;
		}

		private Range GetRange(CodeRange range)
		{
			return new Range()
			{
				start = new Position()
				{
					line = range.Start.LineNo - 1,
					character = range.Start.Row - 1
				},
				end = new Position()
				{
					line = range.End.LineNo - 1,
					character = range.End.Row
				}
			};
		}

		private Location GetLocation(CodeRange range)
		{
			Location loc = new Location()
			{
				range = GetRange(range)
			};
			return loc;
		}

		private Location GetLocation(CodeRange range, ProduireFile rdr)
		{
			Location loc = new Location()
			{
				range = GetRange(range),
				uri = GetUri(rdr)
			};
			return loc;
		}

		private static ResponseError GetError(string msg)
		{
			return new ResponseError() { message = msg };
		}

		protected override VoidResult<ResponseError> Shutdown()
		{
			//Logger.Instance.Log("プロデル言語サーバが終了しました。");
			// WORKAROUND: Language Server does not receive an exit notification.
			Task.Delay(1000).ContinueWith(_ => Environment.Exit(0));
			return VoidResult<ResponseError>.Success();
		}

		internal struct ErrorInfoItem
		{
			internal readonly string Code;
			internal readonly string Description;
			internal readonly BreakRange Range;

			internal ErrorInfoItem(string code, string description, BreakRange range)
			{
				this.Code = code;
				this.Description = description;
				this.Range = range;
			}
		}
	}
}
