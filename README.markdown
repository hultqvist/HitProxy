# HitProxy

Http Proxy with programmable filters.

https://github.com/hultqvist/HitProxy

Ideas of a filter can easily be programmed and added within five minutes.
At runtime filters can further be configures via the WebUI.

Available filters:

  * WebUI - All runtime configuration of filters is done via this filter. Change style by modifying ~/.config/HitProxy/style.css
  * AdBlock - implementation of the popular filtering plugin for Firefox but here usable on all browsers such as Chrome
  * Pass to another proxy: Now implemented to I2P proxy
  * CrossDomain - Block third party request - the heaviest ad- and privacy blocking functionality, but it does require advanced user interaction to make most pages work.
  * UserAgent and Language - Randomized your UserAgent and Accept-Language headers for every request.

There are many more ideas not yet implemented.
Have a look at HitProxy/Filters to get an idea.
	
# Motivation

To have the adblock and filtering features in Firefox extensions available to any browser, thus not limiting browser choice to whether it has extensions or not.

To build a working platform upon new ideas of handling http requests easily can be implemented.

To learn by experience about the http protocol.

# Contact, FeedBack/Bugs, Contrubutions

You can contact me using phq@silentorbit.com.

Public Bugs/Feedback can be sent via https://github.com/hultqvist/HitProxy/issues

Code contributions are sent via email or pull requests on github.

# Licence

All code is licensed under AGPLv3, see COPYING.AGPLv3 for details.
