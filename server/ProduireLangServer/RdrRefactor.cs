// Copyright(C) 2019-2024 utopiat.net https://github.com/utopiat-ire/
using System;
using System.Collections.Generic;
using Produire.Model;
using Produire.Model.Phrase;

namespace Produire.Designer.DocumentModel
{
	/// <summary>
	/// コードモデル内の名前を変更します
	/// </summary>
	internal class RdrRefactor : CodeModelWalker<INamedElement>
	{
		public override bool Walk(IRefactorableElement refactorableElement, INamedElement element)
		{
			bool result = refactorableElement.Rename(element);
			if (refactorableElement is IPhraseContainer)
			{
				if (Walk((refactorableElement as IPhraseContainer).Phrases, element))
					result = true;
			}
			return result;
		}
	}

	/// <summary>
	/// コードモデル内の名前を探します
	/// </summary>
	internal class ReferenceSearcher : CodeModelWalker<IPhrase>
	{
		internal List<Found> Founds = new List<Found>();
		ProduireFile currentRdr;

		public ReferenceSearcher()
		{
		}
		internal void Search(ProduireFile produireFile, IPhrase searchTarget)
		{
			Founds.Clear();
			currentRdr = produireFile;
			produireFile.Treat<IPhrase>(this, searchTarget);
		}

		public override bool Walk(IRefactorableElement refactorableElement, IPhrase element)
		{
			return Walk(refactorableElement as IPhrase, element);
		}

		public override bool Walk(IPhrase phrase, IPhrase element)
		{
			if (phrase is IPhraseContainer)
			{
				return Walk((phrase as IPhraseContainer).Phrases, element);
			}

			bool isMatch = false;
			if (phrase is 変数字句 && element is 変数字句)
			{
				isMatch = ((phrase as 変数字句).Variable == (element as 変数字句).Variable);
			}
			else if (phrase is 種類字句 && element is 種類字句)
			{
				isMatch = ((phrase as 種類字句).GetPType() == (element as 種類字句).GetPType());
			}
			else if (phrase is 動詞字句 && element is 動詞字句)
			{
				isMatch = ((phrase as 動詞字句).Verb == (element as 動詞字句).Verb);
			}
			else if (phrase is 設定項目名字句 && element is 設定項目名字句)
			{
				isMatch = (phrase as 設定項目名字句).PropertyInfo == (element as 設定項目名字句).PropertyInfo;
			}
			if (isMatch)
			{
				Founds.Add(new Found { Phrase = phrase, ProduireFile = currentRdr });
			}
			return isMatch;
		}
	}

	/// <summary>
	/// コードモデル内の変数を探します
	/// </summary>
	internal class PVariableSearcher : CodeModelWalker<PVariable>
	{
		internal List<Found> Founds = new List<Found>();
		ProduireFile currentRdr;

		public PVariableSearcher()
		{
		}
		internal void Search(ProduireFile produireFile, PVariable searchTarget)
		{
			Founds.Clear();
			currentRdr = produireFile;
			produireFile.Treat<PVariable>(this, searchTarget);
		}

		public override bool Walk(IRefactorableElement refactorableElement, PVariable element)
		{
			return Walk(refactorableElement as IPhrase, element);
		}

		public override bool Walk(IPhrase phrase, PVariable element)
		{
			if (phrase is IPhraseContainer)
			{
				return Walk((phrase as IPhraseContainer).Phrases, element);
			}

			bool isMatch = false;
			if (phrase is 変数字句)
			{
				isMatch = (phrase as 変数字句).Variable == element;
			}
			if (isMatch)
			{
				Founds.Add(new Found { Phrase = phrase, ProduireFile = currentRdr });
			}
			return isMatch;
		}
	}

	internal class Found
	{
		internal IPhrase Phrase;
		internal ProduireFile ProduireFile;
	}
}
