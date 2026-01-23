import os
import re

def fix_csproj(filepath):
    with open(filepath, 'r', encoding='utf-8') as f:
        content = f.read()

    original_content = content
    
    # Replace GA.Business.Core with GA.Domain.Core AND GA.Domain.Services
    def repl_business_core(match):
        path = match.group(1) 
        # path is like ..\Common\GA.Business.Core\GA.Business.Core.csproj
        
        # New paths
        path_core = path.replace("GA.Business.Core", "GA.Domain.Core")
        path_services = path.replace("GA.Business.Core", "GA.Domain.Services")
        
        # Return the new lines. matching group 1 is just the path inside quotes.
        # We are replacing the whole Include="..." part essentially?
        # No, re.sub replaces the match.
        
        # The regex matches: Include="...GA.Business.Core.csproj"
        # We want to return: Include="...GA.Domain.Core.csproj" />\n    <ProjectReference Include="...GA.Domain.Services.csproj"
        
        # We need to be careful about the closing tag. The regex only matches the Include attribute.
        # If the original was <ProjectReference Include="..." />
        # We will replace Include="..." with Include="..." />\n<ProjectReference Include="..."
        # But the original string has the closing " at the end of group 1.
        # Wait, group 1 is inside quotes.
        
        # Let's adjust regex to match the whole tag if possible, or just the attribute.
        # Regex: Include="([^\"]*GA\.Business\.Core\.csproj)"
        
        return f'Include="{path_core}" />\r\n    <ProjectReference Include="{path_services}"'

    # Regex for ProjectReference to GA.Business.Core
    # This assumes self-closing tag. If it's not self-closing, it might break.
    # Usually ProjectReference is self-closing.
    # We replace `Include="..."` with `Include="..." /> ... <ProjectReference Include="..."`
    # And we rely on the caller to have ` />` at the end which we will consume or duplicate?
    
    # Better approach: Find the whole ProjectReference line.
    # <ProjectReference Include=".*\GA.Business.Core.csproj" />
    
    regex_whole_tag = r'<ProjectReference\s+Include="([^\"]*GA\.Business\.Core\.csproj)"\s*/>'
    
    def repl_whole_tag(match):
        path = match.group(1)
        indent = match.group(0).split('<')[0] # try to capture indentation? No, match.group(0) is the match. 
        
        # path_core = path.replace("GA.Business.Core", "GA.Domain.Core")
        # path_services = path.replace("GA.Business.Core", "GA.Domain.Services")
        
        # Simple string replacement on path
        p_core = path.replace("GA.Business.Core", "GA.Domain.Core")
        p_services = path.replace("GA.Business.Core", "GA.Domain.Services")
        
        return f'<ProjectReference Include="{p_core}" />\n    <ProjectReference Include="{p_services}" />'

    content = re.sub(regex_whole_tag, repl_whole_tag, content)

    # Now Replace GA.Domain with GA.Domain.Core
    # <ProjectReference Include=".*\GA.Domain.csproj" />
    regex_domain = r'<ProjectReference\s+Include="([^\"]*GA\.Domain\.csproj)"\s*/>'
    
    def repl_domain(match):
        path = match.group(1)
        p_core = path.replace("GA.Domain", "GA.Domain.Core")
        return f'<ProjectReference Include="{p_core}" />'

    content = re.sub(regex_domain, repl_domain, content)

    if content != original_content:
        print(f"Updating {filepath}")
        with open(filepath, 'w', encoding='utf-8') as f:
            f.write(content)

for root, dirs, files in os.walk('.'):
    for file in files:
        if file.endswith('.csproj') or file.endswith('.fsproj'):
            fix_csproj(os.path.join(root, file))
