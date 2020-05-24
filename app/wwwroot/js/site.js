window.addEventListener("load", function () {

    /** @type {NodeListOf<HTMLIFrameElement>} */
    var seamlessFrames = document.querySelectorAll("iframe.seamless")
    for (const frame of seamlessFrames)
    {
        frame.height = frame.contentDocument.documentElement.scrollHeight + "px"

        /** @type {NodeListOf<HTMLAnchorElement>} */
        var links = frame.contentDocument.querySelectorAll("a[href]")
        for (const link of links)
        {
            link.target = "_top"
        }
    }

})
