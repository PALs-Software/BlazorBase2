/*
  Registers the service worker and reloads the page once when a new worker takes over.

  A new worker takes over as soon as it is installed, which can swap the assets underneath a page that booted from the
  previous deployment: the app would then run an old client against a new API. Reloading once on the handover keeps
  the two halves in step. The first install also hands over control, and there is nothing stale to replace then.

  A dialog may hold input that has not been saved. The reload waits until no dialog is open anymore.
*/
(function () {
    if (!navigator.serviceWorker) {
        return;
    }

    var hasDialogOpen = function () {
        return document.querySelector('.base-dialog, dialog[open], [role="dialog"], [role="alertdialog"]') !== null;
    };

    var reloadWhenNoDialogIsOpen = function () {
        if (!hasDialogOpen()) {
            location.reload();
            return;
        }

        var observer = new MutationObserver(function () {
            if (!hasDialogOpen()) {
                observer.disconnect();
                location.reload();
            }
        });

        observer.observe(document.body, { childList: true, subtree: true, attributes: true, attributeFilter: ['open'] });
    };

    navigator.serviceWorker.register('service-worker.js', { updateViaCache: 'none' });

    var wasAlreadyControlled = Boolean(navigator.serviceWorker.controller);
    var hasReloadedForUpdate = false;

    navigator.serviceWorker.addEventListener('controllerchange', function () {
        if (!wasAlreadyControlled || hasReloadedForUpdate) {
            return;
        }

        hasReloadedForUpdate = true;
        reloadWhenNoDialogIsOpen();
    });
})();
