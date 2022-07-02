function seamlessResize() {
  /** @type {NodeListOf<HTMLIFrameElement>} */
  const frames = document.querySelectorAll("iframe.seamless");
  for (const frame of frames) {
    if (!frame.contentDocument) continue;

    /** @type {NodeListOf<HTMLAnchorElement>} */
    const links = frame.contentDocument.querySelectorAll("a[href]");
    /** @type {NodeListOf<HTMLImageElement>} */
    const images = frame.contentDocument.querySelectorAll("img[data-src]");

    frame.height = String(frame.contentDocument.documentElement.offsetHeight);
    for (const link of links) {
      link.target = "_blank";
    }
    for (const image of images) {
      image.addEventListener("load", seamlessLoaded);
      image.referrerPolicy = "no-referrer";
      image.src = image.dataset.src || "";
      delete image.dataset.src;
    }
  }
}

function seamlessLoaded() {
  seamlessResize();
  // if (document.querySelector("ul.messages")) {
  //   messageAutoScroll();
  // }
}

window.addEventListener("resize", seamlessResize);
window.addEventListener("load", seamlessResize);
