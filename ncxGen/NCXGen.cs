﻿//
// NCXGen.cs
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
//   $Rev: 18 $

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.XPath;
using System.Xml.Linq;
using System.IO;
using Mono.Options;


namespace ncxGen
{

    class NCXGen
    {

        //
        // This section contains variables that will be eventually populated from the command line
        //                

        static string TocTitle            = "Table of Contents";    // Title to be put in the html TOC
        static string BookAuthorName      = "Author Name";
        static string BookTitle           = "Book Title";
        static string BookId              = "BookId";
        static int numNcxLevelsToCollapse = 0;            // Number of levels to collapse in the Ncx file
        static int numLevels              = 3;            // Number of levels of the ToC (calculated from the numbers of -q or the default is 3)
        static string textGuide           = null;         // ID-"name" attribute pointing to the starting A TAG of the text.
        static string[] guideItems        = { "text", "start" };
        static bool DEBUG                 = false;

        //
        // Program constants
        //
        static string tocHtmlFilename = "ncx-gen-toc.html";
        static string tocNcxFilename  = "ncx-gen-toc.ncx";
        static string basePath        = @".\";
        public const string Prefix    = "NCXGen";
                        
        /// <summary>
        /// The int pointing at the next available integer for creating the anchors
        /// </summary>
        static int nextID = 0;

        static string SourceFilename;
        
        static void Main(string[] args)
        {   
            List<TOCItem> TOCItems = new List<TOCItem>();
            List<string> unprocessed = null;
            List<string> queriesByLevel = new List<string>();
            var showHelp = false;
            var makeHtmlToc = false;        // Option to generate the html ToC
            var makeNcxToc = false;        // Option to generate the NcX ToC
            var makeOpfToc = false;        // Option to generate the Opf file //TODO: reference to other file names as parameter
            string htmlSource;

            var options = new OptionSet()
            {
                {"h|?|help", "Display this help.", v => showHelp = v != null},
                {"toc", "Generate the html Table of Contents.", v => makeHtmlToc = v != null},
                {"ncx", "Generate the NCX Global Navigation.", v => makeNcxToc = v != null},
                {"opf", "Create the opf file package.", v => makeOpfToc = v != null},
                {"a|all", "Create both html ToC, ncx and opf files.", v => {makeOpfToc=true; makeHtmlToc = true; makeNcxToc = true;}},
                {"q=|query=", "The XPath query to find the ToC items. Use multiple times to add levels in the ToC.", (q) => queriesByLevel.Add(q) },
                {"l=|level=", "Number of levels to collapse to generate the NCX file - used with -ncx or -all.", (int l) => numNcxLevelsToCollapse = l},
                {"toc-title=", "Name of the Table of Contents", v => TocTitle = v},
                {"author=", "Author name.", a => BookAuthorName = a},
                {"title=","Book title.", t => BookTitle = t},
                {"debug", "Show debug messages", v => DEBUG = true}
            };


            try
            {
                unprocessed = options.Parse(args);
            }
            catch (OptionException e)
            {
                Console.WriteLine("ERROR: Parameter format not correct: " + e.Message);
                ShowHelp(options);
                Environment.Exit(1);
            }

            if (showHelp)
            {
                ShowHelp(options);
                return;
            }

            //
            // Filename and wrong parameters handling
            //
            if (unprocessed.Count != 1)
            {
                Console.WriteLine("ERROR: Wrong number of parameters.");
                ShowHelp(options);
                Environment.Exit(1);
            }

            SourceFilename = unprocessed.First<string>();

            if (SourceFilename[0] == '-' || SourceFilename[0] == '/')
            {
                Console.WriteLine("ERROR: Invalid parameter: \"" + SourceFilename + "\"");
                ShowHelp(options);
                Environment.Exit(1);
            }

            if (!File.Exists(SourceFilename))
            {
                Console.WriteLine("ERROR: " + SourceFilename + " File not found.");
                Environment.Exit(1);
            }
            basePath = Path.GetDirectoryName(SourceFilename);

            //
            // Parameters validation (cannot generate opf without both html and ncx ToC)
            //
            if (!(makeHtmlToc || makeNcxToc) && makeOpfToc)
            {
                Console.WriteLine("ERROR: Can't create opf file without both html and ncx Table of Contents.");
                Environment.Exit(1);
            }
            // Setup default query
            if (queriesByLevel.Count == 0)
                queriesByLevel.AddRange(new List<string> { @"h2", @"h3", @"h4" });
            else
                numLevels = queriesByLevel.Count;
            Console.WriteLine("Making "+numLevels+" levels in the toc.");

            File.Copy(SourceFilename, SourceFilename + ".bak", true);       // TODO: remove (shouldn't touch original file

            htmlSource = File.ReadAllText(SourceFilename);
            populateTOC(ref htmlSource, ref TOCItems, queriesByLevel);                        //Create the TOC list in the variable TOCItems
            if (TOCItems.Count == 0)
            {
                Console.WriteLine("ERROR: The query produced no results.");
                Environment.Exit(1);
            }
                            
            //TODO: text guide
            //Console.WriteLine("Found one text guide.");
            
            Console.WriteLine("The TOC will have " + TOCItems.Count + " items.");
            
            if (makeHtmlToc) generateHtmlToc(TOCItems);
            if (makeNcxToc) generateNcxToc(TOCItems, makeHtmlToc);                                    // If we made the html ToC, add it to the NCX root
            if (makeOpfToc) generateOpf(TOCItems);
            
            
            // TODO: Save file to default or -o filename
            //if (makeHtmlToc || makeNcxToc || makeNcxToc) xDoc.Save(filename);               // Save only if one file is created.

        }

        private static void ShowHelp(OptionSet p)
        {
            Console.WriteLine("\nNcx and opf tool generator from a single XHTML file.\n");
            Console.WriteLine("ncxgen [options] filename\n");
            p.WriteOptionDescriptions(Console.Out);
            Console.WriteLine("\nExample:\n\t \"ngen.exe -all -q \"//h1\" -q \"//h2[@class='toc']\" source.xhtml\"");
            Console.WriteLine("This expression will parse the xhtml file source.xhtml looking for the tag h1 and the tag h2 with an attribute class set to 'toc'. It will then create the html Table of Contents, the NCX Global Navigation file and the OPF file using the items found.");
        }

        /// <summary>
        /// Create the html toc.html containing the html TOC of the book
        /// </summary>
        private static void generateHtmlToc(List<TOCItem> TOCItems)
        {
            var docType = new XDocumentType("html", "-//W3C//DTD XHTML 1.0 Transitional//EN", "http://www.w3.org/TR/xhtml1/DTD/xhtml1-transitional.dtd", null);
            XNamespace ns = "http://www.w3.org/1999/xhtml";

            var styles = new XElement(ns + "style",
                            new XText("h1 {text-align: center}\n"),
                            new XText("\tp {text-align: left}\n"));
            for (int i = 0; i < numLevels; i++)
            {
                styles.Add(new XText("\tp.level_" + i + " text-indent: " + i + "em }\n"));   //TODO: Is there any better way to match it with 
            }

            // Create the html file projecting the TOCItems to a series of <p> elements

            var body =
                new XElement(ns + "body",
                                new XElement(ns + "h1", TocTitle,
                                    new XAttribute("class", "tocHead")),
                                from tocItem in TOCItems
                                select
                                new XElement(ns + "p",
                                    new XAttribute("class", "level_" + tocItem.Level),
                                    new XElement(ns + "a", tocItem.Element,
                                        new XAttribute("href", tocItem.Link))));

            body.Element(ns + "p").SetAttributeValue("height", "2em");               //Add a space between the toc title and the first paragraph


            var htmlTOC =
                    new XElement(ns + "html",
                        new XElement(ns + "head",
                            new XElement(ns + "title", TocTitle),
                            new XComment("Styles for the Table of Contents"),
                            styles),
                        body

                     );

            var docHtml = new XDocument(
                docType,
                htmlTOC);

            docHtml.Save(Path.Combine(basePath, tocHtmlFilename));
            Console.WriteLine(tocHtmlFilename + ": Table of Contents created.");
        }


        private static void generateNcxToc(List<TOCItem> TOCItems, bool addHtmlToC = false)
        {
            int playOrder = 0;
            XElement navPoint;
            int ncxLevel;

            XNamespace ns = "http://www.daisy.org/z3986/2005/ncx/";

            XElement navMap = new XElement(ns + "navMap");

            //Crate as many placeholders as number of levels and populate all of them with the navMap root
            List<XElement> nodePlaceholder = new List<XElement>(numLevels + 1);
            while (nodePlaceholder.Count != numLevels + 1)
            {
                nodePlaceholder.Add(navMap);
            }


            // start adding the HTML ToC to the root, keep separate from ToCItems so it's not showing up in the html ToC

            if (addHtmlToC)
            {
                navPoint =
                        new XElement(ns + "navPoint",
                            new XAttribute("class", "TOC"),
                            new XAttribute("id", "TOC"),
                            new XAttribute("playOrder", playOrder++),
                            new XElement(ns + "navLabel",
                                new XElement(ns + "text", TocTitle)),
                            new XElement(ns + "content",
                                new XAttribute("src", tocHtmlFilename)));

                nodePlaceholder[0].Add(navPoint);
                nodePlaceholder[1] = navPoint;
            }

            foreach (TOCItem item in TOCItems)
            {
                navPoint =
                    new XElement(ns + "navPoint",
                        new XAttribute("class", item.Level),
                        new XAttribute("id", item.Id),
                        new XAttribute("playOrder", playOrder++),
                        new XElement(ns + "navLabel",
                            new XElement(ns + "text", item.Element)),
                        new XElement(ns + "content",
                            new XAttribute("src", item.Link)));

                ncxLevel = (item.Level + 1);                            //The levels index start at 0 but in the ncx tree [0] is actually the root of the menu
                ncxLevel = ncxLevel - numNcxLevelsToCollapse;           //

                if (ncxLevel < 1) ncxLevel = 1;                         //Collapse anything no more than the first ncx level

                nodePlaceholder[ncxLevel - 1].Add(navPoint);            //Add the navPoint as a child of the placeholder navPoint present in the previous menu level
                nodePlaceholder[ncxLevel] = navPoint;
            }

            XDocumentType docType = new XDocumentType("ncx", "-//NISO//DTD ncx 2005-1//EN", "http://www.daisy.org/z3986/2005/ncx-2005-1.dtd", null);

            XElement ncx = new XElement(ns + "ncx",
                                new XAttribute("version", "2005-1"),
                                new XAttribute(XNamespace.Xml + "lang", "en-US"),
                                new XElement(ns + "head",
                                    new XElement(ns + "meta",
                                        new XAttribute("name", "dtb:uid"),
                                        new XAttribute("content", BookId)),
                                    new XElement(ns + "meta",
                                        new XAttribute("name", "dtb:depth"),
                                        new XAttribute("content", numLevels - numNcxLevelsToCollapse)),
                                    new XElement(ns + "meta",
                                        new XAttribute("name", "dtb:totalPageCount"),
                                        new XAttribute("content", "0")),
                                    new XElement(ns + "meta",
                                        new XAttribute("name", "dtb:maxPageNumber"),
                                        new XAttribute("content", "0"))),
                                new XElement(ns + "docTitle",
                                    new XElement(ns + "text", BookTitle)),
                                new XElement(ns + "docAuthor",
                                    new XElement(ns + "text", BookAuthorName)),
                                navMap);

            XDocument ncxDoc = new XDocument(docType, ncx);

            ncxDoc.Save(Path.Combine(basePath, tocNcxFilename));
            Console.WriteLine(tocHtmlFilename + ": NCX Global Navigation file created.");
        }

        /// <summary>
        /// Write to console the TOC, for testing only.
        /// </summary>
        private static void printTOC(List<TOCItem> TOCItems)
        {
            string prefix;

            foreach (TOCItem item in TOCItems)
            {
                prefix = new string(' ', item.Level);
                Console.WriteLine(prefix + item.Element + "\t" + item.Link);
            }
        }

        /// <summary>
        /// Generate the TOC based on the given query.
        /// </summary>
        /// <param name="htmlFile">The html source text</param>
        /// <param name="TOCItems">The list of TAC items</param>
        /// <param name="searchQuery">The list with the search queries</param>
        /// <returns>An integer with the number of occurrences of searcQuery found in htmlText</returns>
        private static void populateTOC(ref string htmlText, ref List<TOCItem> TOCItems , List<string> searchQuery)
        {               
            for (int level = 0; level < searchQuery.Count(); level++)
            {
                string pattern = generateTAGQuery(searchQuery[level]);
                if (DEBUG) {Console.WriteLine(pattern);}
                MatchCollection matches = Regex.Matches(htmlText, pattern, RegexOptions.IgnoreCase);
                foreach (Match match in matches)
                {
                    TOCItems.Add(new TOCItem(match.Index,
                                            match.Groups["ELEMENT"].Value,
                                            Path.GetFileName(SourceFilename), 
                                            ++nextID, 
                                            level
                                            ));
                }
            }
            // Order the final node list by document position
            TOCItems.Sort();
            return;
        }

        private static void generateOpf(List<TOCItem> TOCItems)
        {
            XNamespace ns = "http://www.idpf.org/2007/opf";

            XNamespace dc = "http://purl.org/dc/elements/1.1/";

            XNamespace opf = "http://www.idpf.org/2007/opf";

            XElement metadata =

                    new XElement(ns + "metadata",                                    //TODO : setup all metadata
                        new XAttribute(XNamespace.Xmlns + "dc", dc),
                        new XAttribute(XNamespace.Xmlns + "opf", opf),
                        new XElement(dc + "title", BookTitle),
                        new XElement(dc + "language", "en-us"),
                        new XElement("meta",
                            new XAttribute("name", "cover"),
                            new XAttribute("content", "My_Cover")),
                        new XElement(dc + "identifier",
                            new XAttribute("id", BookId),
                            new XAttribute(opf + "scheme", "ISBN"),
                            "123456789"),
                        new XElement(dc + "creator", BookAuthorName),
                        new XElement(dc + "publisher", "amazon.com"),
                        new XElement(dc + "subject", "amazon.com"),
                        new XElement(dc + "date", DateTime.Today));

            XElement manifest =
                new XElement(ns + "manifest",
                    new XElement(ns + "item",
                        new XAttribute("id", "item1"),
                        new XAttribute("media-type", "application/xhtml+xml"),
                        new XAttribute("href", tocHtmlFilename)),
                    new XElement(ns + "item",
                        new XAttribute("id", "item2"),
                        new XAttribute("media-type", "application/xhtml+xml"),
                        new XAttribute("href", Path.GetFileName(SourceFilename))),
                    new XElement(ns + "item",
                        new XAttribute("id", "My_Table_of_Contents"),
                        new XAttribute("media-type", "application/x-dtbncx+xml"),
                        new XAttribute("href", tocNcxFilename)),
                    new XElement(ns + "item",
                        new XAttribute("id", "My_Cover"),
                        new XAttribute("media-type", "image/jpeg"),
                        new XAttribute("href", "Cover.jpg")));

            XElement spine =
                new XElement(ns + "spine",
                    new XAttribute("toc", "My_Table_of_Contents"),
                    new XElement(ns + "itemref",
                        new XAttribute("idref", "item1")),
                    new XElement(ns + "itemref",
                        new XAttribute("idref", "item2")));

            XElement guide =
                new XElement(ns + "guide",
                    new XElement(ns + "reference",
                        new XAttribute("type", "toc"),
                        new XAttribute("title", TocTitle),
                        new XAttribute("href", tocHtmlFilename)),
                    new XElement(ns + "reference",
                        new XAttribute("type", "text"),
                        new XAttribute("title", "Beginning"),
                        new XAttribute("href",
                            Path.GetFileName(SourceFilename) +
                            ((textGuide == null) ? "" : "#" + textGuide)
                        )
                    )
                );

            XElement package =
                new XElement(ns + "package",
                    new XAttribute("version", "2.0"),
                    new XAttribute("unique-identifier", BookId),
                    metadata, manifest, spine, guide);

            XDocument opfDoc = new XDocument(package);

            opfDoc.Save(Path.ChangeExtension(SourceFilename, ".opf"));
            Console.WriteLine(Path.ChangeExtension(SourceFilename, ".opf" + ": file created."));
        }

        private static string generateTAGQuery(string searchQuery)
        {
            return @"<" + 
                @"(?:" + searchQuery + @")" + 
                @"\s*(?<ATTRIBUTES>[^>]*)>" +       // Matches the first part of the HTML tag, grouping the attributes apart
                @"(?<ELEMENT>.*)" +                 // Matches the element and names it
                @"(?:</" +
                @"(?:" + searchQuery + @")" + 
                @"[^>]*>)";                         // Closing tag discarded
            
        }
    }
}
