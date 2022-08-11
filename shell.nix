# ########################################################################
#
#                 -- Generated with omgcmd --
#      (do not edit unless you know what you're doing)
#
#########################################################################

{ pkgs ? import ./default.nix { } }:
let
  nixpkgs = pkgs.nixpkgs;
  omgpkgs = pkgs.omgpkgs;
  dotnet-sdk = with nixpkgs.dotnetCorePackages;
    combinePackages [
      aspnetcore_5_0
      aspnetcore_6_0
      sdk_3_1
      sdk_5_0
      sdk_6_0
      runtime_5_0
      runtime_6_0
    ];
in with nixpkgs;
mkShell {
  buildInputs = [
    awscli2
    azure-storage-azcopy
    azure-cli
    bash
    boundary
    cacert
    cloc
    consul
    curl
    direnv
    docker
    docker-compose
    dos2unix
    dotnet-sdk
    git
    git-lfs
    github-cli
    glibcLocales
    gnumake
    haskellPackages.ShellCheck
    jq
    nixfmt
    nix-prefetch-git
    nomad
    openssh
    p7zip
    (python3.withPackages (ps: [ ps.setuptools ps.autopep8 ]))
    ripgrep
    sec
    shfmt
    terraform_1_0
    which
    wget
    vault
  ];
}
