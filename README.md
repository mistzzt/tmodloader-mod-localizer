# Mod Localizer for tModLoader

Mod localizer is a tool developed for players to create their own localized mod easily; it support mods of [tModLoader][tml] v0.10 or higher version.

## Introduction

As far as we know, tModLoader has added a new feature enabling mod to have its translations. However, it cannot be easily used since it needs you to know how to code and it only works with open-source mod.

But this tool solves these problems of tModLoader's internationalization feature:

* It can read `.tmod` files and generate game content (like *item names, tooltips and buff descriptions*) in **JSON format** in which format you can translate them into your language with comfort.

* After your translating, the tool will use your translation to modify contents inside .tmod files regardless whether it is open source.

* It doesn't require programming knowledges; all you have to know is how to use command line and edit files using editors like [VSCode][vscode].

## Usage

### `ModLocalizer --mode dump <Mod Name>` 

**Example: `ModLocalizer --mode dump ExampleMod.tmod`**

Program will create a new folder which name is the same as that of mod file and output mod contents inside it.

### `ModLocalizer --mode patch --folder <content path> --language <language> [Mod file]`

**Example: `ModLocalizer --mode patch --folder ExampleModContent --language Chinese ExampleMod.tmod`**

Modify *ExampleMod.tmod* according to contents in `ExampleModContent` folder.

The folder option is mandatory when the program mode is **patch**, because program needs it to modify mod file.

You have seven languages for chosing:

* English
* German
* Italian
* French
* Spanish
* Russian
* Chinese (This is default language if you don't specify one using `--langage`)
* Portuguese
* Polish

They're case-sensitive: you must enter them as what exactly they are here.

If I chose **Chinese**, then my translations will only show when I use Chinese language in *Terraria Settings*.
So you must carefully choose your language to let your translations work.

### For further usage, enter `ModLocalizer --help`.

## How to translate mod content

You might get something like this when translating mod:

```json
  {
    "TypeName": "DO-NOT-EDIT-ME", // DO NOT EDIT THIS
    "Namespace": "DO-NOT-EDIT-ME", // DO NOT EDIT THIS
    "Name": "Example Breastplate",
    "ToolTip": "This is a modded body armor.\nImmunity to 'On Fire!'\n+20 max mana and +1 max minions",
    "ModifyTooltips": []
  }
```

You can modify anything except `TypeName`, `Method` and `Namespace`.

### File/Directory inside mode content folder

- `Info.json` contains build properties like mod version and descriptions.

- `ModInfo.json` contains properties of `*.tmod` files like mod name or version.

- `Items` contains item properties like item name and tooltip.

- `NPCs` contains NPC properties like name, chat texts and shop button texts.

- `Tiles` contains map entries which need to be translated.

- `Miscs` contains some other texts.

[vscode]: https://code.visualstudio.com/
[tml]: https://forums.terraria.org/index.php?threads/1-3-tmodloader-a-modding-api.23726/