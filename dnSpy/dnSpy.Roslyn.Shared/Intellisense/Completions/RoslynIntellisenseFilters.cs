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
using System.Reflection;
using dnSpy.Contracts.Images;
using dnSpy.Contracts.Language.Intellisense;
using dnSpy.Roslyn.Shared.Properties;
using Microsoft.CodeAnalysis.Completion;

namespace dnSpy.Roslyn.Shared.Intellisense.Completions {
	static class RoslynIntellisenseFilters {
		public static RoslynIntellisenseFilter[] CreateFilters() => new RoslynIntellisenseFilter[] {
			new RoslynIntellisenseFilter("Local", dnSpy_Roslyn_Shared_Resources.LocalsAndParametersToolTip, "L", CompletionTags.Local, CompletionTags.Parameter),
			new RoslynIntellisenseFilter("Literal", dnSpy_Roslyn_Shared_Resources.ConstantsToolTip, "O", CompletionTags.Constant),
			new RoslynIntellisenseFilter("Property", dnSpy_Roslyn_Shared_Resources.PropertiesToolTip, "P", CompletionTags.Property),
			new RoslynIntellisenseFilter("Event", dnSpy_Roslyn_Shared_Resources.EventsToolTip, "V", CompletionTags.Event),
			new RoslynIntellisenseFilter("Field", dnSpy_Roslyn_Shared_Resources.FieldsToolTip, "F", CompletionTags.Field),
			new RoslynIntellisenseFilter("Method", dnSpy_Roslyn_Shared_Resources.MethodsToolTip, "M", CompletionTags.Method),
			new RoslynIntellisenseFilter("ExtensionMethod", dnSpy_Roslyn_Shared_Resources.ExtensionMethodsToolTip, "X", CompletionTags.ExtensionMethod),
			new RoslynIntellisenseFilter("Interface", dnSpy_Roslyn_Shared_Resources.InterfacesToolTip, "I", CompletionTags.Interface),
			new RoslynIntellisenseFilter("Class", dnSpy_Roslyn_Shared_Resources.ClassesToolTip, "C", CompletionTags.Class),
			new RoslynIntellisenseFilter("Module", dnSpy_Roslyn_Shared_Resources.ModulesToolTip, "U", CompletionTags.Module),
			new RoslynIntellisenseFilter("Struct", dnSpy_Roslyn_Shared_Resources.StructuresToolTip, "S", CompletionTags.Structure),
			new RoslynIntellisenseFilter("Enum", dnSpy_Roslyn_Shared_Resources.EnumsToolTip, "E", CompletionTags.Enum),
			new RoslynIntellisenseFilter("Delegate", dnSpy_Roslyn_Shared_Resources.DelegatesToolTip, "D", CompletionTags.Delegate),
			new RoslynIntellisenseFilter("Namespace", dnSpy_Roslyn_Shared_Resources.NamespacesToolTip, "N", CompletionTags.Namespace),
		};
	}

	sealed class RoslynIntellisenseFilter : IntellisenseFilter {
		static readonly Assembly imageAssembly = typeof(RoslynIntellisenseFilter).Assembly;

		public string[] Tags { get; }

		public RoslynIntellisenseFilter(string imageName, string toolTip, string accessKey, params string[] tags)
			: base(new ImageReference(imageAssembly, imageName), toolTip, accessKey) {
			if (tags == null)
				throw new ArgumentNullException(nameof(tags));
			if (tags.Length == 0)
				throw new ArgumentOutOfRangeException(nameof(tags));
			Tags = tags;
		}
	}
}
