/* LogoSwap */
(function() {
    'use strict';

    var customLogoUrl = __LOGO_URL__;
    var pending = false;

    function replaceLogos() {
        pending = false;
        document.querySelectorAll('.pageTitleWithLogo, .pageTitleWithDefaultLogo, .mainDrawer .logoImage, .navDrawerLogo, .adminDrawerLogo').forEach(function(el) {
            el.style.backgroundImage = 'url("' + customLogoUrl + '")';
            el.style.backgroundSize = 'contain';
            el.style.backgroundRepeat = 'no-repeat';
            el.style.backgroundColor = 'transparent';
        });

        document.querySelectorAll('img[src*="banner-light"], img[src*="banner-dark"], img[src*="icon-transparent"], img.imgLogoIcon').forEach(function(img) {
            if (img.getAttribute('src') !== customLogoUrl) {
                img.setAttribute('src', customLogoUrl);
            }
        });
    }

    function scheduleReplacement() {
        if (!pending) {
            pending = true;
            setTimeout(replaceLogos, 50);
        }
    }

    function start() {
        replaceLogos();
        new MutationObserver(scheduleReplacement).observe(document.body, {
            childList: true,
            subtree: true,
            attributes: true,
            attributeFilter: ['class', 'src']
        });
    }

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', start, { once: true });
    } else {
        start();
    }

    ['viewshow', 'pageshow', 'viewbeforeshow'].forEach(function(event) {
        document.addEventListener(event, scheduleReplacement);
    });
})();
/* END LogoSwap */
