/*
 * Show Director for Jellyfin Web
 * ----------------------------------
 * Injected via a <script> tag added to index.html by the ShowDirector
 * server plugin. Watches the DOM for library cards as they're rendered,
 * looks up each item's director from Jellyfin's own /Items API, and
 * inserts a line for it between the title and the year.
 */
(function () {
    "use strict";

    var THIS_SCRIPT = document.currentScript;

    var CONFIG = {
        separator: (THIS_SCRIPT && THIS_SCRIPT.dataset.separator) || ", ",
        maxDirectors: parseInt((THIS_SCRIPT && THIS_SCRIPT.dataset.maxDirectors) || "2", 10),
        applyMovies: !THIS_SCRIPT || THIS_SCRIPT.dataset.applyMovies !== "false",
        applySeries: !THIS_SCRIPT || THIS_SCRIPT.dataset.applySeries !== "false",
        applyEpisodes: !!(THIS_SCRIPT && THIS_SCRIPT.dataset.applyEpisodes === "true")
    };

    var ALLOWED_TYPES = [];
    if (CONFIG.applyMovies) ALLOWED_TYPES.push("Movie");
    if (CONFIG.applySeries) ALLOWED_TYPES.push("Series");
    if (CONFIG.applyEpisodes) ALLOWED_TYPES.push("Episode");

    // itemId -> director string (or null if none / not a supported type)
    var directorCache = new Map();
    // itemId -> true while a lookup is in flight, to avoid duplicate fetches
    var pending = new Set();

    var PROCESSED_ATTR = "data-director-checked";

    function log() {
        if (window.localStorage && window.localStorage.getItem("ShowDirectorDebug") === "1") {
            console.log.apply(console, ["[ShowDirector]"].concat(Array.prototype.slice.call(arguments)));
        }
    }

    function waitForApiClient(callback) {
        if (window.ApiClient) {
            callback();
            return;
        }
        var interval = setInterval(function () {
            if (window.ApiClient) {
                clearInterval(interval);
                callback();
            }
        }, 250);
    }

    // Jellyfin Web's card markup has varied a bit across releases, but a
    // card element consistently carries the item id somewhere in its
    // attributes and has one or more ".cardText" lines underneath the
    // poster (title, then secondary info like the year).
    function extractItemId(card) {
        return (
            card.getAttribute("data-id") ||
            card.getAttribute("data-itemid") ||
            (card.querySelector("[data-id]") && card.querySelector("[data-id]").getAttribute("data-id"))
        );
    }

    function getCardTextLines(card) {
        // cardBox / cardScalable wraps the poster; text lines usually live
        // in a sibling ".cardText" container underneath it, OR nested
        // directly inside the card for some layouts.
        var container = card.querySelector(".cardText, .cardText-first")
            ? card
            : card;
        return container.querySelectorAll(".cardText");
    }

    function insertDirectorLine(card, directorText) {
        if (!directorText) {
            return;
        }
        if (card.querySelector(".ShowDirectorLine")) {
            return; // already inserted
        }

        var textLines = getCardTextLines(card);
        if (!textLines || textLines.length === 0) {
            return;
        }

        // Convention in Jellyfin Web: first .cardText line = title,
        // subsequent line(s) = secondary info (year, episode count, etc).
        // We insert our new line right after the title line, so the
        // result reads Title / Director / Year.
        var titleLine = textLines[0];
        var newLine = document.createElement("div");
        newLine.className = "cardText cardText-secondary ShowDirectorLine";
        newLine.style.overflow = "hidden";
        newLine.style.textOverflow = "ellipsis";
        newLine.style.whiteSpace = "nowrap";
        newLine.style.textAlign = "center";
        newLine.textContent = directorText;

        titleLine.insertAdjacentElement("afterend", newLine);
    }

    function formatDirectors(people) {
        if (!people || !people.length) {
            return null;
        }
        var directors = people
            .filter(function (p) {
                return p.Type === "Director";
            })
            .map(function (p) {
                return p.Name;
            });

        if (!directors.length) {
            return null;
        }

        if (directors.length > CONFIG.maxDirectors) {
            return directors.slice(0, CONFIG.maxDirectors).join(CONFIG.separator) + ", et al.";
        }

        return directors.join(CONFIG.separator);
    }

    function fetchAndApply(card, itemId) {
        if (directorCache.has(itemId)) {
            insertDirectorLine(card, directorCache.get(itemId));
            return;
        }
        if (pending.has(itemId)) {
            return;
        }
        pending.add(itemId);

        var userId = ApiClient.getCurrentUserId();

        ApiClient.getItem(userId, itemId)
            .then(function (item) {
                pending.delete(itemId);

                if (ALLOWED_TYPES.indexOf(item.Type) === -1) {
                    directorCache.set(itemId, null);
                    return;
                }

                var directorText = formatDirectors(item.People);
                directorCache.set(itemId, directorText);
                insertDirectorLine(card, directorText);
            })
            .catch(function (err) {
                pending.delete(itemId);
                log("Failed to fetch item", itemId, err);
            });
    }

    function processCard(card) {
        if (card.hasAttribute(PROCESSED_ATTR)) {
            // Even if already checked once, the card DOM node can be
            // recycled/reused by the virtual scroller for a different
            // item id. Re-validate the id matches what we last saw.
            var seenId = card.getAttribute(PROCESSED_ATTR);
            var currentId = extractItemId(card);
            if (seenId === currentId) {
                return;
            }
            // Different item now occupying this node - clear the old
            // director line and re-process.
            var old = card.querySelector(".ShowDirectorLine");
            if (old) {
                old.remove();
            }
        }

        var itemId = extractItemId(card);
        if (!itemId) {
            return;
        }

        card.setAttribute(PROCESSED_ATTR, itemId);
        fetchAndApply(card, itemId);
    }

    function scanForCards(root) {
        if (!root || !root.querySelectorAll) {
            return;
        }
        var cards = root.querySelectorAll(".card");
        for (var i = 0; i < cards.length; i++) {
            processCard(cards[i]);
        }
    }

    function init() {
        // Initial pass over whatever's already rendered.
        scanForCards(document.body);

        // Cards are added/removed constantly as the user navigates and
        // scrolls (virtual scrolling). A MutationObserver on the whole
        // body catches all of that without hooking into Jellyfin's
        // internal router/view events.
        var observer = new MutationObserver(function (mutations) {
            for (var i = 0; i < mutations.length; i++) {
                var mutation = mutations[i];
                for (var j = 0; j < mutation.addedNodes.length; j++) {
                    var node = mutation.addedNodes[j];
                    if (node.nodeType !== 1) {
                        continue;
                    }
                    if (node.classList && node.classList.contains("card")) {
                        processCard(node);
                    }
                    scanForCards(node);
                }
            }
        });

        observer.observe(document.body, { childList: true, subtree: true });

        log("initialized", CONFIG);
    }

    waitForApiClient(function () {
        if (document.readyState === "loading") {
            document.addEventListener("DOMContentLoaded", init);
        } else {
            init();
        }
    });
})();
