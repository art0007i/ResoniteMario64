import os
#import re

# Define the replacement mappings
replacements = {
    r'Vector3': r'float3',
    r'Vector2': r'float2',
    r'Quaternion': r'floatQ',
    r'Mathf': r'MathX',
    r'Kafe.CVRSuperMario64': r'ResoniteMario64',
    r'MelonLogger.Warning': r'ResoniteMario64.Warn',
    r'MelonLogger': r'ResoniteMario64',
}

# Get the current working directory as the root directory
root_directory = os.getcwd()

# Define the file extensions to search for
file_extensions = ['.cs', '.csproj', '.md']

# Iterate through files and perform replacements
for foldername, subfolders, filenames in os.walk(root_directory):
    for filename in filenames:
        if filename.endswith(tuple(file_extensions)):
            filepath = os.path.join(foldername, filename)
            with open(filepath, 'r', encoding='utf-8') as file:
                content = file.read()
            
            # Perform replacements in the content
            for old_text, new_text in replacements.items():
                content = content.replace(old_text, new_text)
                #chat gpt used regex but u can just do replace wtf
                #re.sub(old_text, new_text, content)
            
            # Write the modified content back to the file
            with open(filepath, 'w', encoding='utf-8') as file:
                file.write(content)

print(f"Replacements completed in {root_directory}.")