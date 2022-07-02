import { $, getElements } from "../modules/elements.js";

const e = getElements();
const d = Object.create(null);

if (e.favourites && e.favourites.list) {
  addList();
} else {
  addToggle();
  updateToggle();
}

function addList() {
  const pages = JSON.parse(localStorage["favourites"] || "[]");
  for (const page of pages) {
    const li = $("li", $("a", { href: page.link }, page.title));
    e.favourites.list.append(li);
  }
}

function addToggle() {
  d.toggleIcon = $("i", {
    class: "favourite-this-page bi bi-star text-blue",
    title: "Make this page a favourite page",
  });
  d.toggleIcon.addEventListener("click", onToggle);
  const h1 = document.querySelector("h1");
  h1?.insertAdjacentElement("beforebegin", d.toggleIcon);

  d.page = {
    link: [location.pathname, location.search].join(""),
    title: document.title.replace(
      " - " + document.querySelector(".navbar-brand")?.textContent,
      ""
    ),
  };
}

function updateToggle() {
  const pages = JSON.parse(localStorage["favourites"] || "[]");
  const present = pages.some((page) => page.link === d.page.link);
  d.toggleIcon.classList.toggle("bi-star", !present);
  d.toggleIcon.classList.toggle("bi-star-fill", present);
}

function onToggle() {
  const pages = JSON.parse(localStorage["favourites"] || "[]");
  if (pages.some((page) => page.link === d.page.link)) {
    localStorage["favourites"] = JSON.stringify(
      pages.filter((page) => page.link !== d.page.link)
    );
  } else {
    localStorage["favourites"] = JSON.stringify(pages.concat(d.page));
  }
  updateToggle();
}
