using System;
using System.Collections.Generic;
using UnityEngine;

// Owns reusable views for one collection, including filtering and ordering.
public sealed class ContentEntryList<T, TView> where T : class where TView : Component
{
    readonly Func<TView> create;
    readonly Func<T, string> name;
    readonly Action<T, TView, bool> bind;
    readonly Dictionary<T, TView> views = new Dictionary<T, TView>();

    public ContentEntryList(Func<TView> create, Func<T, string> name, Action<T, TView, bool> bind)
    {
        this.create = create; this.name = name; this.bind = bind;
    }

    public int Refresh(IReadOnlyList<T> entries, string query, T selected)
    {
        var retained = new HashSet<T>(entries);
        var removed = new List<T>();
        foreach (T entry in views.Keys) if (!retained.Contains(entry)) removed.Add(entry);
        foreach (T entry in removed) Remove(entry);
        int visibleCount = 0;
        foreach (T entry in entries)
        {
            if (!views.TryGetValue(entry, out TView view) || view == null)
            {
                view = create();
                views[entry] = view;
            }
            bool visible = name(entry).IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0;
            view.gameObject.SetActive(visible);
            if (!visible) continue;
            view.transform.SetAsLastSibling();
            Update(entry, selected);
            visibleCount++;
        }
        return visibleCount;
    }

    public void Update(T entry, T selected)
    {
        if (entry == null || !views.TryGetValue(entry, out TView view) || view == null) return;
        bind(entry, view, ReferenceEquals(entry, selected));
    }

    public TView Get(T entry) => entry != null && views.TryGetValue(entry, out TView view) ? view : null;
    public void Remove(T entry)
    {
        if (entry == null || !views.TryGetValue(entry, out TView view)) return;
        views.Remove(entry);
        if (view != null) { view.gameObject.SetActive(false); UnityEngine.Object.Destroy(view.gameObject); }
    }
    public void Clear()
    {
        foreach (TView view in views.Values)
            if (view != null) { view.gameObject.SetActive(false); UnityEngine.Object.Destroy(view.gameObject); }
        views.Clear();
    }
}

// The full-screen libraries use the same card layout and selection behavior.
public sealed class ContentCardList<T> where T : class
{
    readonly ContentEntryList<T, PrefabLibraryCard> entries;
    public ContentCardList(PrefabLibraryCard template, Transform parent, Func<T, string> name,
        Func<T, Sprite> preview, Func<T, bool> builtIn, Action<T> select)
    {
        entries = new ContentEntryList<T, PrefabLibraryCard>(
            () => UnityEngine.Object.Instantiate(template, parent, false), name,
            (item, card, selected) => card.Configure(name(item), preview(item), builtIn(item), selected, () => select(item)));
    }
    public int Refresh(IReadOnlyList<T> items, string query, T selected) => entries.Refresh(items, query, selected);
    public void Update(T item, T selected) => entries.Update(item, selected);
    public PrefabLibraryCard Get(T item) => entries.Get(item);
    public void Remove(T item) => entries.Remove(item);
    public void Clear() => entries.Clear();
}
