# ZanJhat Map

Adds a minimap and world map with customizable markers to help players navigate and track important locations.

---

## Features
- Minimap display for real-time navigation
- Full world map support
- Custom markers system
- Highly customizable map colors
- Depends on ZanJhat Core for full functionality and integration

---

## How to add custom block colors

To customize block colors for ZanJhat Map, create a `.bpd` file.

### Step 1: Create file
Create a file with extension:
```
.bpd
```

Example:
```
MyBlockColors.bpd
```

---

### Step 2: File structure

```xml
<Blocks>
    <Block Name="DirtBlock" NeedChange="false" Color="132,106,58" />
</Blocks>
```

---

### Attributes

#### Name
- The block class name
- Example: `DirtBlock`, `GrassBlock`

#### Color
- RGB color used on the map
- Format: `R,G,B`
- Example: `132,106,58`

#### NeedChange
- `true`: Color can be modified by environment systems
  - grass color map
  - water color map
  - leaves color
  - custom mod handlers
- `false`: Color is fixed

---

### Example

```xml
<Blocks>
    <Block Name="DirtBlock" NeedChange="false" Color="132,106,58" />
    <Block Name="GrassBlock" NeedChange="true" Color="171,171,171" />
    <Block Name="OakWoodBlock" NeedChange="false" Color="198,154,83" />
</Blocks>
```

---

## Notes
- Ensure block names match exact class names in-game
- Invalid RGB values may cause default fallback colors
- Requires ZanJhat Core to function correctly
