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
using dnSpy.Contracts.Text;
using dnSpy.Contracts.Text.Editor;

namespace dnSpy.Text.Editor.Operations {
	static class EditorOptionsExtensions {
		public static string GetLineBreak(this IEditorOptions editorOptions, SnapshotPoint pos) {
			if (editorOptions.GetOptionValue(DefaultOptions.ReplicateNewLineCharacterOptionId)) {
				var line = pos.GetContainingLine();
				if (line.LineBreakLength != 0)
					return pos.Snapshot.GetText(line.Extent.End.Position, line.LineBreakLength);
				if (line.LineNumber != 0) {
					line = pos.Snapshot.GetLineFromLineNumber(line.LineNumber - 1);
					return pos.Snapshot.GetText(line.Extent.End.Position, line.LineBreakLength);
				}
			}
			var linebreak = editorOptions.GetOptionValue(DefaultOptions.NewLineCharacterOptionId);
			return linebreak.Length != 0 ? linebreak : Environment.NewLine;
		}
	}
}