// VolumeManager.mm (Objective-C++)
#import <Foundation/Foundation.h>
#import <UIKit/UIKit.h>
#import <UniformTypeIdentifiers/UniformTypeIdentifiers.h>

extern "C" {
    bool IsVolumeMounted(const char* volumePath) {
        NSString *path = [NSString stringWithUTF8String:volumePath];
        return [[NSFileManager defaultManager] fileExistsAtPath:path];
    }

    void SelectVolume() {
        dispatch_async(dispatch_get_main_queue(), ^{
            UIDocumentPickerViewController *picker = [[UIDocumentPickerViewController alloc]
                initWithDocumentTypes:@[(NSString *)UTTypeFolder.identifier]
                inMode:UIDocumentPickerModeOpen];
            picker.allowsMultipleSelection = NO;
            picker.delegate = (id<UIDocumentPickerDelegate>)[[UIApplication sharedApplication] delegate];

            UIViewController *rootViewController = [UIApplication sharedApplication].keyWindow.rootViewController;
            [rootViewController presentViewController:picker animated:YES completion:nil];
        });
    }
}
