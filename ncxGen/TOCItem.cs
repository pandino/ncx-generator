﻿//
// TOCItem.cs
//
// Authors:
//  Giorgio Ceolin <genma@megane.it>
//
// Copyright (C) 2011 Giorgio Ceolin
//
//   This program is free software: you can redistribute it and/or modify
//   it under the terms of the GNU General Public License as published by
//   the Free Software Foundation, either version 3 of the License, or
//   (at your option) any later version.
//
//   This program is distributed in the hope that it will be useful,
//   but WITHOUT ANY WARRANTY; without even the implied warranty of
//   MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
//   GNU General Public License for more details.
//
//   You should have received a copy of the GNU General Public License
//   along with this program.  If not, see <http://www.gnu.org/licenses/>.
//
//   $Rev: 13 $

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using HtmlAgilityPack;


namespace ncxGen
{
    class TOCItem : IComparable<TOCItem>
    {   
        /// <summary>
        /// Value to put in the TOC line
        /// </summary>
        public String Element { get; private set; }

        /// <summary>
        /// The name used as anchor in the id attribute
        /// </summary>
        public string Id { get; private set; }

        /// <summary>
        /// Full name of the source html, with path and extension.
        /// </summary>
        private string Filename { get; set; }

        private HtmlNode node { get; set; }

        /// <summary>
        /// Html link to the node tag. Style: "filename.ext#link"
        /// </summary>
        public String Link
        {
            get
            {
                return Filename + "#" + Id;
            }
            private set { }
        }

        /// <summary>
        /// Level of the node in the TOC
        /// </summary>
        public int Level { get; private set; }
                
        /// <summary>
        /// Create a TOC item and write the id attribute in the associated XElement
        /// </summary>
        /// <param name="element">Node element in the original html file</param>
        /// <param name="value">Text to be written in the TOC</param>
        /// <param name="link">Link to the anchor in the html file</param>
        /// <param name="level">Level of indentation in the TOC</param>
        public TOCItem(HtmlNode node, string filename, string id,  int level)
        {
            Element = node.InnerText.Trim();
            Filename    = filename;
            Id          = id;                                
            Level       = level;
            this.node   = node;                        
        }
        
        public int CompareTo(TOCItem other)
        {
            if (this.node.Line < other.node.Line) return -1;
            else if (this.node.Line > other.node.Line) return 1;
            else if (this.node.LinePosition < other.node.LinePosition) return -1;
            else if (this.node.LinePosition > other.node.LinePosition) return 1;
            else return 0;
        }
    }
}
