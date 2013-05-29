param($installPath, $toolsPath, $package, $project)

function Fix-ReferencesCopyLocal($package, $project)
{
    $asms = $package.AssemblyReferences | %{$_.Name}

    foreach ($reference in $project.Object.References)
    {
        if ($asms -contains $reference.Name + ".dll")
        {
            if($reference.CopyLocal -eq $false)
            {
                $reference.CopyLocal = $true;
            }
        }
    }
}

Fix-ReferencesCopyLocal $package $project