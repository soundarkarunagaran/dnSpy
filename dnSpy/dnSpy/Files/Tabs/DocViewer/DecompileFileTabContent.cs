﻿/*
    Copyright (C) 2014-2016 de4dot@gmail.com

    This file is part of dnSpy

    dnSpy is free software: you can redistribute it and/or modify
    it under the terms of the GNU General Public License as published by
    the Free Software Foundation, either version 3 of the License, or
    (at your option) any later version.

    dnSpy is distributed in the hope that it will be useful,
    but WITHOUT ANY WARRANTY; without even the implied warranty of
    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
    GNU General Public License for more details.

    You should have received a copy of the GNU General Public License
    along with dnSpy.  If not, see <http://www.gnu.org/licenses/>.
*/

using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Windows.Threading;
using dnSpy.Contracts.Decompiler;
using dnSpy.Contracts.Files;
using dnSpy.Contracts.Files.Tabs;
using dnSpy.Contracts.Files.Tabs.DocViewer;
using dnSpy.Contracts.Files.TreeView;
using dnSpy.Contracts.Settings;
using dnSpy.Contracts.Text;
using dnSpy.Properties;
using Microsoft.VisualStudio.Utilities;

namespace dnSpy.Files.Tabs.DocViewer {
	[Export, ExportFileTabContentFactory(Order = TabConstants.ORDER_DECOMPILEFILETABCONTENTFACTORY)]
	sealed class DecompileFileTabContentFactory : IFileTabContentFactory {
		public IFileManager FileManager { get; }
		public IFileTreeNodeDecompiler FileTreeNodeDecompiler { get; }
		public IDecompilerManager DecompilerManager { get; }
		public IDecompilationCache DecompilationCache { get; }
		public IMethodAnnotations MethodAnnotations { get; }
		public IContentTypeRegistryService ContentTypeRegistryService { get; }
		public Lazy<IDocumentViewerCustomDataProvider, IDocumentViewerCustomDataProviderMetadata>[] DocumentViewerCustomDataProviders { get; }

		[ImportingConstructor]
		DecompileFileTabContentFactory(IFileManager fileManager, IFileTreeNodeDecompiler fileTreeNodeDecompiler, IDecompilerManager decompilerManager, IDecompilationCache decompilationCache, IMethodAnnotations methodAnnotations, IContentTypeRegistryService contentTypeRegistryService, [ImportMany] IEnumerable<Lazy<IDocumentViewerCustomDataProvider, IDocumentViewerCustomDataProviderMetadata>> documentViewerCustomDataProviders) {
			this.FileManager = fileManager;
			this.FileTreeNodeDecompiler = fileTreeNodeDecompiler;
			this.DecompilerManager = decompilerManager;
			this.DecompilationCache = decompilationCache;
			this.MethodAnnotations = methodAnnotations;
			this.ContentTypeRegistryService = contentTypeRegistryService;
			this.DocumentViewerCustomDataProviders = documentViewerCustomDataProviders.OrderBy(a => a.Metadata.Order).ToArray();
		}

		public IFileTabContent Create(IFileTabContentFactoryContext context) =>
			new DecompileFileTabContent(this, context.Nodes, DecompilerManager.Decompiler);

		public IFileTabContent Create(IFileTreeNodeData[] nodes) =>
			new DecompileFileTabContent(this, nodes, DecompilerManager.Decompiler);

		static readonly Guid GUID_SerializedContent = new Guid("DE0390B0-747C-4F53-9CFF-1D10B93DD5DD");

		public Guid? Serialize(IFileTabContent content, ISettingsSection section) {
			var dc = content as DecompileFileTabContent;
			if (dc == null)
				return null;

			section.Attribute("Language", dc.Decompiler.UniqueGuid);
			return GUID_SerializedContent;
		}

		public IFileTabContent Deserialize(Guid guid, ISettingsSection section, IFileTabContentFactoryContext context) {
			if (guid != GUID_SerializedContent)
				return null;

			var langGuid = section.Attribute<Guid?>("Language") ?? DecompilerConstants.LANGUAGE_CSHARP;
			var language = DecompilerManager.FindOrDefault(langGuid);
			return new DecompileFileTabContent(this, context.Nodes, language);
		}
	}

	sealed class DecompileFileTabContent : IAsyncFileTabContent, IDecompilerTabContent {
		readonly DecompileFileTabContentFactory decompileFileTabContentFactory;
		readonly IFileTreeNodeData[] nodes;

		public IDecompiler Decompiler { get; set; }

		public DecompileFileTabContent(DecompileFileTabContentFactory decompileFileTabContentFactory, IFileTreeNodeData[] nodes, IDecompiler decompiler) {
			this.decompileFileTabContentFactory = decompileFileTabContentFactory;
			this.nodes = nodes;
			this.Decompiler = decompiler;
		}

		public IFileTabContent Clone() =>
			new DecompileFileTabContent(decompileFileTabContentFactory, nodes, Decompiler);
		public IFileTabUIContext CreateUIContext(IFileTabUIContextLocator locator) =>
			locator.Get<IDocumentViewer>();

		public string Title {
			get {
				if (nodes.Length == 0)
					return dnSpy_Resources.EmptyTabTitle;
				if (nodes.Length == 1)
					return nodes[0].ToString(Decompiler);
				var sb = new StringBuilder();
				foreach (var node in nodes) {
					if (sb.Length > 0)
						sb.Append(", ");
					sb.Append(node.ToString(Decompiler));
				}
				return sb.ToString();
			}
		}

		public object ToolTip {
			get {
				if (nodes.Length == 0)
					return null;
				return Title;
			}
		}

		public IFileTab FileTab {
			get { return fileTab; }
			set {
				if (value == null)
					throw new ArgumentNullException(nameof(value));
				if (fileTab == null)
					fileTab = value;
				else if (fileTab != value)
					throw new InvalidOperationException();
			}
		}
		IFileTab fileTab;

		public IEnumerable<IFileTreeNodeData> Nodes => nodes;

		sealed class DecompileContext {
			public DecompileNodeContext DecompileNodeContext;
			public DocumentViewerContent CachedContent;
			public CancellationTokenSource CancellationTokenSource;
			public object SavedRefPos;
		}

		DecompileContext CreateDecompileContext(IShowContext ctx) {
			var decompileContext = new DecompileContext();
			var decompilationContext = new DecompilationContext();
			decompilationContext.CalculateBinSpans = true;
			decompilationContext.GetDisableAssemblyLoad = () => decompileFileTabContentFactory.FileManager.DisableAssemblyLoad();
			decompilationContext.IsBodyModified = m => decompileFileTabContentFactory.MethodAnnotations.IsBodyModified(m);
			var output = new DocumentViewerOutput();
			var dispatcher = Dispatcher.CurrentDispatcher;
			decompileContext.DecompileNodeContext = new DecompileNodeContext(decompilationContext, Decompiler, output, dispatcher);
			if (ctx.IsRefresh) {
				decompileContext.SavedRefPos = ((IDocumentViewer)ctx.UIContext).SaveReferencePosition();
				if (decompileContext.SavedRefPos != null) {
					ctx.OnShown = e => {
						if (e.Success && !e.HasMovedCaret) {
							e.HasMovedCaret = ((IDocumentViewer)ctx.UIContext).RestoreReferencePosition(decompileContext.SavedRefPos);
							if (!e.HasMovedCaret) {
								((IDocumentViewer)ctx.UIContext).MoveCaretToPosition(0);
								e.HasMovedCaret = true;
							}
						}
					};
				}
			}
			return decompileContext;
		}

		void UpdateLanguage() {
			if (FileTab.IsActiveTab)
				decompileFileTabContentFactory.DecompilerManager.Decompiler = Decompiler;
		}

		public void OnSelected() => UpdateLanguage();
		public void OnUnselected() { }
		public void OnHide() { }

		public void OnShow(IShowContext ctx) {
			UpdateLanguage();
			var decompileContext = CreateDecompileContext(ctx);
			IContentType contentType;
			decompileContext.CachedContent = decompileFileTabContentFactory.DecompilationCache.Lookup(decompileContext.DecompileNodeContext.Decompiler, nodes, out contentType);
			decompileContext.DecompileNodeContext.ContentType = contentType;
			ctx.UserData = decompileContext;
		}

		public void AsyncWorker(IShowContext ctx, CancellationTokenSource source) {
			var decompileContext = (DecompileContext)ctx.UserData;
			decompileContext.CancellationTokenSource = source;
			decompileContext.DecompileNodeContext.DecompilationContext.CancellationToken = source.Token;
			decompileFileTabContentFactory.FileTreeNodeDecompiler.Decompile(decompileContext.DecompileNodeContext, nodes);
		}

		public void EndAsyncShow(IShowContext ctx, IAsyncShowResult result) {
			var decompileContext = (DecompileContext)ctx.UserData;
			var documentViewer = (IDocumentViewer)ctx.UIContext;

			var contentType = decompileContext.DecompileNodeContext.ContentType;
			if (contentType == null) {
				var contentTypeString = decompileContext.DecompileNodeContext.ContentTypeString;
				if (contentTypeString == null)
					contentTypeString = ContentTypes.TryGetContentTypeStringByExtension(decompileContext.DecompileNodeContext.Decompiler.FileExtension) ?? ContentTypes.PlainText;
				contentType = decompileFileTabContentFactory.ContentTypeRegistryService.GetContentType(contentTypeString);
				Debug.Assert(contentType != null);
			}

			DocumentViewerContent content;
			if (result.IsCanceled) {
				var docViewerOutput = new DocumentViewerOutput();
				docViewerOutput.Write(dnSpy_Resources.DecompilationCanceled, BoxedTextColor.Error);
				content = CreateContent(documentViewer, docViewerOutput);
			}
			else if (result.Exception != null) {
				var docViewerOutput = new DocumentViewerOutput();
				docViewerOutput.Write(dnSpy_Resources.DecompilationException, BoxedTextColor.Error);
				docViewerOutput.WriteLine();
				docViewerOutput.Write(result.Exception.ToString(), BoxedTextColor.Text);
				content = CreateContent(documentViewer, docViewerOutput);
			}
			else {
				content = decompileContext.CachedContent;
				if (content == null) {
					var docViewerOutput = (DocumentViewerOutput)decompileContext.DecompileNodeContext.Output;
					content = CreateContent(documentViewer, docViewerOutput);
					if (docViewerOutput.CanBeCached)
						decompileFileTabContentFactory.DecompilationCache.Cache(decompileContext.DecompileNodeContext.Decompiler, nodes, content, contentType);
				}
			}

			if (result.CanShowOutput)
				documentViewer.SetContent(content, contentType);
		}

		sealed class DocumentViewerCustomDataContext : IDocumentViewerCustomDataContext, IDisposable {
			public IDocumentViewer DocumentViewer { get; private set; }
			Dictionary<string, object> customDataDict;
			Dictionary<string, object> resultDict;

			public string Text { get; }

			public DocumentViewerCustomDataContext(IDocumentViewer documentViewer, string text, Dictionary<string, object> customDataDict) {
				DocumentViewer = documentViewer;
				Text = text;
				this.customDataDict = customDataDict;
				this.resultDict = new Dictionary<string, object>(StringComparer.Ordinal);
			}

			internal Dictionary<string, object> GetResultDictionary() => resultDict;

			public void AddCustomData(string id, object data) {
				if (customDataDict == null)
					throw new ObjectDisposedException(nameof(IDocumentViewerCustomDataContext));
				if (id == null)
					throw new ArgumentNullException(nameof(id));
				if (resultDict.ContainsKey(id))
					throw new InvalidOperationException(nameof(AddCustomData) + "() can only be called once with the same " + nameof(id));
				resultDict.Add(id, data);
			}

			public TData[] GetData<TData>(string id) {
				if (customDataDict == null)
					throw new ObjectDisposedException(nameof(IDocumentViewerCustomDataContext));
				if (id == null)
					throw new ArgumentNullException(nameof(id));

				object listObj;
				if (!customDataDict.TryGetValue(id, out listObj))
					return Array.Empty<TData>();
				var list = (List<TData>)listObj;
				return list.ToArray();
			}

			public void Dispose() {
				customDataDict = null;
				DocumentViewer = null;
				resultDict = null;
			}
		}

		DocumentViewerContent CreateContent(IDocumentViewer documentViewer, DocumentViewerOutput docViewerOutput) {
			using (var context = new DocumentViewerCustomDataContext(documentViewer, docViewerOutput.GetCachedText(), docViewerOutput.GetCustomDataDictionary())) {
				foreach (var lazy in decompileFileTabContentFactory.DocumentViewerCustomDataProviders)
					lazy.Value.OnCustomData(context);
				return docViewerOutput.CreateResult(context.GetResultDictionary());
			}
		}

		public bool CanStartAsyncWorker(IShowContext ctx) {
			var decompileContext = (DecompileContext)ctx.UserData;
			if (decompileContext.CachedContent != null)
				return false;

			var uiCtx = (IDocumentViewer)ctx.UIContext;
			uiCtx.ShowCancelButton(dnSpy_Resources.Decompiling, () => decompileContext.CancellationTokenSource.Cancel());
			return true;
		}
	}
}
