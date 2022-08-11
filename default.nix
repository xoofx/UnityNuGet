# ########################################################################
#
#                 -- Generated with omgcmd --
#      (do not edit unless you know what you're doing)
#
#########################################################################

let
  # Look here for information about how to generate `nixpkgs-version.json`.
  #  → https://nixos.wiki/wiki/FAQ/Pinning_Nixpkgs
  pinnedVersions =
    builtins.fromJSON (builtins.readFile ./.nixpkgs-version.json);
  pinnedNixpkgs = import
    (builtins.fetchGit { inherit (pinnedVersions.nixpkgs) url rev ref; }) {
      config = { allowUnfree = true; };
    };
  pinnedOmgpkgs = import
    (builtins.fetchGit { inherit (pinnedVersions.omgpkgs) url rev ref; }) {
      pkgs = pinnedNixpkgs;
    };

  # This allows overriding pkgs by passing `--arg pkgs ...`
in { pkgs ? pinnedNixpkgs }: {
  nixpkgs = pkgs;
  omgpkgs = pinnedOmgpkgs;
}

