mergeInto(LibraryManager.library, {
    SyncPrefabStorage: function () {
        if (Module.orbitPrefabSyncInProgress) {
            Module.orbitPrefabSyncPending = true;
            return;
        }

        function flush() {
            Module.orbitPrefabSyncInProgress = true;
            Module.orbitPrefabSyncPending = false;
            FS.syncfs(false, function (error) {
                Module.orbitPrefabSyncInProgress = false;
                if (error)
                    console.error("Could not persist saved prefabs:", error);
                if (Module.orbitPrefabSyncPending)
                    flush();
            });
        }
        flush();
    }
});
