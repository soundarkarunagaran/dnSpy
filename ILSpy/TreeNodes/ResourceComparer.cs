﻿/*
    Copyright (C) 2014-2015 de4dot@gmail.com

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
using dnlib.DotNet;
using dnlib.DotNet.Resources;

namespace ICSharpCode.ILSpy.TreeNodes {
	sealed class ResourceComparer : IComparer<Resource> {
		public static readonly ResourceComparer Instance = new ResourceComparer();

		public int Compare(Resource x, Resource y) {
			int c = StringComparer.OrdinalIgnoreCase.Compare(x.Name, y.Name);
			if (c != 0)
				return c;
			return x.MDToken.Raw.CompareTo(y.MDToken.Raw);
		}
	}

	sealed class ResourceElementComparer : IComparer<ResourceElement> {
		public static readonly ResourceElementComparer Instance = new ResourceElementComparer();

		public int Compare(ResourceElement x, ResourceElement y) {
			int c = StringComparer.OrdinalIgnoreCase.Compare(x.Name, y.Name);
			if (c != 0)
				return c;
			int cx = (int)x.ResourceData.Code.FixUserType();
			int cy = (int)y.ResourceData.Code.FixUserType();
			return cx.CompareTo(cy);
		}
	}

	static class ResourceTypeCodeExtensions {
		public static ResourceTypeCode FixUserType(this ResourceTypeCode code) {
			if (code < ResourceTypeCode.UserTypes)
				return code;
			return ResourceTypeCode.UserTypes;
		}
	}
}
