// Copyright(C) 2019-2024 utopiat.net https://github.com/utopiat-ire/
using LanguageServer.Parameters.TextDocument;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace ProduireLangServer
{
    public class TextDocumentChangedEventArgs : EventArgs
    {
        private readonly TextDocumentItem _document;

        public TextDocumentChangedEventArgs(TextDocumentItem document)
        {
            _document = document;
        }

        public TextDocumentItem Document => _document;
    }
}
