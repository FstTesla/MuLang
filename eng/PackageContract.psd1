@{
    Packages = @(
        @{
            Id = 'MuLang.Core'
            Project = 'src\MuLang.Core\MuLang.Core.csproj'
            Dependencies = @()
        }
        @{
            Id = 'MuLang.IR'
            Project = 'src\MuLang.IR\MuLang.IR.csproj'
            Dependencies = @('MuLang.Core')
        }
        @{
            Id = 'MuLang.Compiler'
            Project = 'src\MuLang.Compiler\MuLang.Compiler.csproj'
            Dependencies = @('MuLang.IR')
        }
        @{
            Id = 'MuLang.Exporters.DotNet'
            Project = 'src\MuLang.Exporters.DotNet\MuLang.Exporters.DotNet.csproj'
            Dependencies = @('MuLang.IR')
        }
        @{
            Id = 'MuLang.StandardLibrary'
            Project = 'src\MuLang.StandardLibrary\MuLang.StandardLibrary.csproj'
            Dependencies = @('MuLang.Core')
        }
        @{
            Id = 'MuLang.StandardLibrary.DotNet'
            Project = 'src\MuLang.StandardLibrary.DotNet\MuLang.StandardLibrary.DotNet.csproj'
            Dependencies = @(
                'MuLang.Exporters.DotNet'
                'MuLang.StandardLibrary'
            )
        }
    )
    ConsumerPackages = @(
        'MuLang.Compiler'
        'MuLang.Exporters.DotNet'
    )
}
