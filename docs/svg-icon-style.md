# SVG icon notes

Use the existing files in `Assets/_Game/Icons/SVGs` as the style reference. `Undo.svg` is an example of the approved outlined-arrow style; check the relevant icons for other shapes. Keep the SVG editable and use a 1000 × 1000 canvas. The background outside the artwork is transparent.

For icons using the circular border, its black line is 100 units wide with a 50-unit white line centered over it. Match that visible width in the icon's main lines: 50 units of white with 25 units of black on each side, for 100 units total. These are visible widths, not necessarily the `stroke-width` values on a filled shape. For example, a 75-unit-wide white filled ribbon with a centered 25-unit black stroke has a 50-unit visible white interior and a 100-unit overall width. Increasing the stroke without widening the ribbon can cover its white interior.

Make the symbol large and readable within the border while keeping those line widths consistent. When changing the size of the symbol, adjust its geometry rather than scaling its line thickness. A horizontally mirrored arrow can serve as redo.

For new icons, create an SVG in the same folder and leave the existing source files untouched unless asked to edit them. Render a temporary preview and inspect it at both a useful editing size and a small button size before finishing; the user renders the final PNG. Inkscape is installed at `C:\Program Files\Inkscape\bin\inkscape.com` and can export a preview with `--export-filename` and `--export-width`.
