from pathlib import Path
import xml.etree.ElementTree as E
root=Path(__file__).resolve().parents[2]
files=list((root/'Defs').glob('*.xml'))
assert files,'Missing gameplay Defs'
nodes=[n for f in files for n in E.parse(f).getroot() if n.get('Abstract')!='True']
defs={n.findtext('defName'):n for n in nodes}
required=['TL_WritingTable','TL_Bookcase','TL_UniversalBook','TL_MindScroll','TL_LongBook','TL_ShortBook','TL_WriteBook','TL_Writing','TL_Reading','TL_Read','TL_ApplySpecialBook']
for name in required: assert name in defs,'Missing '+name
assert len([n for n in nodes if n.tag=='RecipeDef'])==1,'Writing must only produce universal book'
assert defs['TL_ReadWorkGiver'].findtext('scanThings')=='false','Held book selection must use a non-scan job; native scanner skips unspawned books'
assert defs['TL_Bookcase'].findtext('size')=='(2,1)'
assert defs['TL_WriteBook'].find('products/TL_UniversalBook').text=='1'
assert defs['TL_LongBook'].find('.//effect').text=='RemoveBrawler'
assert defs['TL_ShortBook'].find('.//effect').text=='AddBrawler'
for name in ['TL_MindScroll','TL_LongBook','TL_ShortBook']:
 assert defs[name].get('ParentName')=='TL_SpecialBookBase','Special books cannot inherit readable BookBase'
assert (root/'Assemblies/TalentLibrary.dll').exists(),'Missing compiled DLL'
for n in E.parse(root/'About/About.xml').getroot().find('supportedVersions'): assert n.text=='1.6'
print('PASS XML gameplay contract:',len(files),'files;',len(defs),'definitions')
