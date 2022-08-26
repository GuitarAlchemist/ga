namespace GA.WebBlazorApp.Components.Grids.Common;

public abstract record GridLoaderProgressUpdate;

public sealed record BeginUpdate(string Text = null) : GridLoaderProgressUpdate;
public sealed record CountUpdate(int? Count) : GridLoaderProgressUpdate;
public sealed record IndexUpdate(int Index) : GridLoaderProgressUpdate;
public sealed record EndUpdate : GridLoaderProgressUpdate;