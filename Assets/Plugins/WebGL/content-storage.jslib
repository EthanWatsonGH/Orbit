mergeInto(LibraryManager.library, {
    SyncContentStorage: function () {
        if (Module.orbitContentSyncInProgress) {
            Module.orbitContentSyncPending = true;
            return;
        }

        function flush() {
            Module.orbitContentSyncInProgress = true;
            Module.orbitContentSyncPending = false;
            FS.syncfs(false, function (error) {
                Module.orbitContentSyncInProgress = false;
                if (error)
                    console.error("Could not persist saved content:", error);
                if (Module.orbitContentSyncPending)
                    flush();
            });
        }
        flush();
    }
});
