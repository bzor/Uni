#ifndef LOOKING_GLASS_SETTINGS
#define LOOKING_GLASS_SETTINGS

// view count is defined in two places in entire project, here and in MultiviewData.cs
#ifdef GEN_VIEWS_ON
	#define LKG_VIEWCOUNT 24
#else
	#define LKG_VIEWCOUNT 48
#endif

#endif
