- allow output filename:
 - if no file specified: all files default to basename ncxgen_out.xxx
- toc and ncx filenames: source_toc.html source.ncx
- remove references to multiple html source filenames (doesn't make any sense)
- *Regex* big overhaul
- validate queries (tag name + optional class only)
- preserve existing ID attributes, option to ignore them
- text start guide items
- TOCItems: 
	- _Element_ (was Value, stripped from HTML)
	- Id (generated if not already exist)
	- _Filename + Link_ (maybe ok)
	- <del>_Level_</del>
	- <del>_Position_ = start position of the tag matched to be used for comparison</del>
	- <del>_CompareTo_ = update to use Position</del>