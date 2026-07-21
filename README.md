# <img width="60" height="60" alt="app-icon" src="https://github.com/user-attachments/assets/bdb7a34c-d86d-488c-92a2-2afd08ca9d06" /> Office Inventory System 
Simple offline inventory system made with Avalonia UI
---
  <img width="1365" height="709" alt="image" src="https://github.com/user-attachments/assets/68574ea4-f051-4d20-9f10-09c764348a14" />
  <img width="1366" height="700" alt="image" src="https://github.com/user-attachments/assets/598ded95-90c9-42fc-95dc-4d8324ebdc87" />


# Manage Database
## Adding Inventory Entry
- By clicking the `Add New Item` button or pressing `Ctrl + N`, it will pop-up a modal for adding the item details
  <img width="533" height="575" alt="image" src="https://github.com/user-attachments/assets/97b187c6-222c-4a26-bc9b-a746cfb8e77e" />
- Clicking the `Delete Selected` button or pressing the `Delete` key will delete all selected cells in the grid
  <img width="448" height="224" alt="image" src="https://github.com/user-attachments/assets/f6c4206a-c1d1-4e81-b86c-0aebae81f9c1" />
- Double clicking the cell will open the edit modal to edit the item details
  <img width="580" height="651" alt="image" src="https://github.com/user-attachments/assets/e6be7475-e1b2-45b4-8edb-1f7a9235dd43" />

## Transactions
- `Add Delivery (In)` `Ctrl + I` button or the stock-in button will open stock-in modal to add new item in the selected item in the inventory grid.

  <img width="463" height="499" alt="image" src="https://github.com/user-attachments/assets/d7a78b66-eb89-4ee7-99fb-145b88be7e21" />
- `Note:` This modal automatically selects the item code when you open it, you can click the comboBox to re-select other items in the list
  <img width="505" height="582" alt="image" src="https://github.com/user-attachments/assets/e8c930de-95bf-40dc-acc3-4e8490abec01" />
- `Log Request (out)` `Ctrl + O` button or stock-out button will open the stock-out modal, same mechanics with the stock-in button
  <img width="472" height="506" alt="image" src="https://github.com/user-attachments/assets/692651c3-6428-4333-9fec-a5bda05341ab" />

## System Utilities
- the `Export` `Ctrl + Shift + E` button will export all the data into a preformatted .xlsx file
- <img width="416" height="196" alt="image" src="https://github.com/user-attachments/assets/7b85368f-3d1e-4e4c-8b33-9091c81c298d" />

- `Import` `Ctrl + Shift + I` button will import the .xlsx file into the inventory system
- `Note:` the import function is strict at the moment and it needs exact column number for the data to be imported
- You can also import the file via drag & drop.
- <img width="527" height="328" alt="image" src="https://github.com/user-attachments/assets/7fc58fa3-062b-4a99-acb3-23c62eb2b0fe" />
- <img width="298" height="111" alt="image" src="https://github.com/user-attachments/assets/b6520f42-6ff2-49fd-bff3-f2717dfb3b28" />
- <img width="1305" height="417" alt="image" src="https://github.com/user-attachments/assets/20347597-7d9a-4de0-9e90-1545e84215d0" />
- <img width="922" height="421" alt="image" src="https://github.com/user-attachments/assets/55004052-bc6b-4a96-9f3f-f920ced8d940" />
  
# Search Functions
- The search bar `Ctrl + L` will provide search clips to easily find the exact entry from the Inventory or the Stock grid
  <img width="586" height="86" alt="image" src="https://github.com/user-attachments/assets/c4fd92d0-ec2f-4781-a8bb-ae11e51c46f5" />

# Todo:
- [x] Simple CRUD functions
- [x] Stock In/Out Panel
- [x] Direct cell edit
- [x] Export Data
- [x] Import Data
- [x] App Icon
- [x] Revamp UI
- [x] Cross Platform (Windows, Macos, Linux)
- [x] Automatic stock calculation
- [ ] cell drag to re-arrange inventory/log index
