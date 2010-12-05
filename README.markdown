# HitProxy

Http Proxy with programmable filters.

https://github.com/hultqvist/HitProxy

Ideas of a filter can easily be programmed and added within five minutes.
At runtime filters can be added, removed and configured via the WebUI.

Available filters:

  * WebUI - Yes all runtime configuration of filters is done via this filter. Change style by copying style.css to $HOME/.config/HitProxy/style.css
  * AdBlock - implementation of the popular filtering plugin for Firefox but here usable on all browsers such as Chrome
  * Custom 404 messages for all sites
  * Pass to another proxy: Now implemented to I2P proxy
  * Referer - Block third party request - the heaviest ad- and privacy blocking functionality, but it does require advanced user interaction to make most pages work.
  * UserAgent and Language - Randomized your UserAgent and Accept-Language headers for every request.
  * Cookie - Basic third party and cross domain blocking, similar configuration as with Referer will come here.

There are many more ideas not yet implemented.
Have a look at HitProxy/Filters to get an idea.
	
# Motivation

To have the adblock and filtering features in Firefox extensions available to any browser, thus not limiting browser choice to whether it has extensions or not.

To build a working platform upon new ideas of handling http requests easily can be implemented.

To learn by experience about the http protocol.

# Contact, FeedBack/Bugs, Contrubutions

You can reach me at phq@endnode.se.

Public Bugs/Feedback can be sent via https://github.com/hultqvist/HitProxy/issues

Code contributions are sent via email or pull requests on github.

# Licence

All code is licensed under GPLv3, see COPYING.Gplv3 for details.
